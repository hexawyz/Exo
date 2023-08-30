using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Exo;

/// <summary>Helpers to create factory methods for drivers.</summary>
/// <remarks>
/// Usage of this class is recommended to provide efficient access to driver factory methods.
/// It will do all the necessary parsing and validation, and allow injecting optional parameters into the Driver.CreateAsync method.
/// </remarks>
public static class DriverFactory
{
	public enum ValidationErrorCode
	{
		None = 0,
		CreateAsyncMethodNotFound = 1,
		InvalidReturnType = 2,
		NoCancellationToken = 3,
		RequiredParameterMismatch = 4,
		UnallowedByRefParameter = 5,
		OptionalParameterNotFound = 6,
		OptionalParameterTypeMismatch = 7,
	}

	public readonly struct ValidationResult
	{
		public ValidationResult(Type driverType, ValidationErrorCode errorCode, string? argument)
		{
			DriverType = driverType;
			ErrorCode = errorCode;
			Argument = argument;
		}

		public Type DriverType { get; }
		public ValidationErrorCode ErrorCode { get; }
		public string? Argument { get; }

		public void ThrowIfFailed()
		{
			if (ErrorCode != ValidationErrorCode.None)
			{
				throw new InvalidOperationException(ErrorCode.FormatError(DriverType, Argument));
			}
		}
	}

	private readonly struct FactoryParameter
	{
		public FactoryParameter(string name, Type type)
		{
			Name = name;
			Type = type;
		}

		public string Name { get; }
		public Type Type { get; }
	}

	private readonly struct FactoryMethodParseResult
	{
		public FactoryMethodParseResult(Type driverType, MethodInfo createAsyncMethod, ParameterInfo[] parameters)
			: this(driverType, createAsyncMethod, parameters, ValidationErrorCode.None, null)
		{
		}

		public FactoryMethodParseResult(Type driverType, ValidationErrorCode validationErrorCode, string? validationErrorArgument)
			: this(driverType, null, null, validationErrorCode, validationErrorArgument)
		{
		}

		private FactoryMethodParseResult(Type driverType, MethodInfo? createAsyncMethod, ParameterInfo[]? parameters, ValidationErrorCode validationErrorCode, string? validationErrorArgument)
		{
			DriverType = driverType;
			CreateAsyncMethod = createAsyncMethod;
			MethodParameters = parameters;
			ValidationErrorCode = validationErrorCode;
			ValidationErrorArgument = validationErrorArgument;
		}

		public Type DriverType { get; }
		public MethodInfo? CreateAsyncMethod { get; }
		public ParameterInfo[]? MethodParameters { get; }
		public ValidationErrorCode ValidationErrorCode { get; }
		public string? ValidationErrorArgument { get; }

		[MemberNotNull(nameof(CreateAsyncMethod))]
		[MemberNotNull(nameof(MethodParameters))]
		public void ThrowIfFailed()
		{
			if (ValidationErrorCode != ValidationErrorCode.None)
			{
				throw new InvalidOperationException(ValidationErrorCode.FormatError(DriverType, ValidationErrorArgument));
			}
			else if (CreateAsyncMethod is null || MethodParameters is null)
			{
				throw new InvalidOperationException();
			}
		}
	}

	private static readonly MethodInfo CompleteMethodInfo = typeof(DriverFactory).GetMethod(nameof(Complete), BindingFlags.NonPublic | BindingFlags.Static)!;

	private static readonly MethodInfo CreateLoggerMethodInfo =
		typeof(LoggerFactoryExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == nameof(LoggerFactoryExtensions.CreateLogger) && m.GetParameters().Length == 1 && m.IsGenericMethod && m.GetGenericArguments().Length == 1);

	private static readonly string LoggerFactoryParameterName = Naming.MakeCamelCase(nameof(IDriverCreationContext<IDriverCreationResult>.LoggerFactory));

	private static Type? GetOptionalBaseType(Type type)
	{
		if (type.IsValueType) return null;

		Type? current = type;
		while (current is not null && current != typeof(object))
		{
			if (current.IsGenericType && Equals(current.GetGenericTypeDefinition(), typeof(Optional<>)))
			{
				return current;
			}
			else
			{
				current = type.BaseType;
			}
		}
		return null;
	}

	private static Type? GetLoggerCategory(Type type)
	{
		if (type.IsValueType) return null;

		if (type.IsGenericType && Equals(type.GetGenericTypeDefinition(), typeof(ILogger<>)))
		{
			return type.GetGenericArguments()[0];
		}

		return null;
	}

	// Compare types by name.
	private static bool Equals(Type a, Type b)
		=> a.FullName == b.FullName &&
			(a.Assembly.FullName == b.Assembly.FullName/* || AreCompatibleAssemblies(a.Assembly, b.Assembly)*/);

	//private static bool AreCompatibleAssemblies(Assembly a, Assembly b)
	//	=> AreCompatibleAssemblies(a.GetName(), b.GetName());

	//private static bool AreCompatibleAssemblies(AssemblyName a, AssemblyName b)
	//	=> a.Name == b.Name && a.CultureName == b.CultureName && a.GetPublicKeyToken().AsSpan().SequenceEqual(b.GetPublicKeyToken());

	private static Dictionary<string, PropertyInfo> ParseContext(Type type)
	{
		var allowedProperties = new Dictionary<string, PropertyInfo>();
		foreach (var property in type.GetProperties())
		{
			allowedProperties.Add(Naming.MakeCamelCase(property.Name), property);
		}
		if (!allowedProperties.TryGetValue(LoggerFactoryParameterName, out var loggerFactoryProperty) || loggerFactoryProperty.PropertyType != typeof(ILoggerFactory))
		{
			throw new ArgumentException("Driver creation context must expose the LoggerFactoryProperty of type ILoggerFactory.");
		}
		return allowedProperties;
	}

	private static FactoryParameter[] ParseFactoryDelegate(Type delegateType, Type contextType, Type resultType)
	{
		var invokeMethod = delegateType.GetMethod("Invoke") ?? throw new InvalidOperationException($"The type {delegateType} is nto a valid delegate type.");
		var parameters = invokeMethod.GetParameters();
		if (invokeMethod.ReturnType != typeof(Task<>).MakeGenericType(resultType) ||
			parameters.Length < 2 ||
			parameters[^1] is not { Name: "cancellationToken", ParameterType.IsByRef: false } ctParameter ||
			ctParameter.ParameterType != typeof(CancellationToken) ||
			parameters[^2] is not { Name: "context", ParameterType.IsByRef: false } pParameter ||
			pParameter.ParameterType != contextType)
		{
			throw new ArgumentException($"The delegate type {delegateType} does not have a valid signature.");
		}

		var fixedParameters = new FactoryParameter[parameters.Length - 2];

		for (int i = 0; i < fixedParameters.Length; i++)
		{
			var parameter = parameters[i];
			if (parameter.Name is not { Length: > 0 } parameterName || !Naming.StartsWithLowerCase(parameterName)) throw new ArgumentException($"Parameter {parameter.Name} is not camel-cased.");
			if (parameter.ParameterType.IsByRef) throw new ArgumentException($"The parameter {parameter.Name} is passed by reference.");
			fixedParameters[i] = new(parameterName, parameter.ParameterType);
		}

		return fixedParameters;
	}

	// NB: This method cannot compare most Type references because it must work with types loaded from MetadataLoadContext.
	// For all types that can be referenced with typeof(), the Equals(Type, Type) method must be used.
	private static FactoryMethodParseResult ParseFactoryMethod(Type driverType, FactoryParameter[] factoryParameters, Dictionary<string, PropertyInfo> availableParameters)
	{
		var createAsyncMethod = driverType.GetMethod("CreateAsync", BindingFlags.Static | BindingFlags.Public);

		if (createAsyncMethod is null) return new(driverType, ValidationErrorCode.CreateAsyncMethodNotFound, null);

		if (createAsyncMethod.ReturnType.IsByRef ||
			!createAsyncMethod.ReturnType.IsGenericType ||
			!Equals(createAsyncMethod.ReturnType.GetGenericTypeDefinition(), typeof(Task<>)) ||
			createAsyncMethod.ReturnType.GetGenericArguments() is not { } taskParameters ||
			taskParameters[0] != driverType)
		{
			return new(driverType, ValidationErrorCode.InvalidReturnType, null);
		}

		var methodParameters = createAsyncMethod.GetParameters();

		// At the minimum, the CreateAsync method must include all the required factory parameters, plus a cancellation token parameter.
		if (methodParameters.Length < factoryParameters.Length + 1) throw new InvalidOperationException($"The method {driverType}.CreateAsync method does not have enough parameters.");

		if (methodParameters[^1] is not { Name: "cancellationToken", ParameterType.IsByRef: false } lastParameter || !Equals(lastParameter.ParameterType, typeof(CancellationToken)))
		{
			return new(driverType, ValidationErrorCode.NoCancellationToken, null);
		}

		int i = 0;
		for (; i < factoryParameters.Length; i++)
		{
			var methodParameter = methodParameters[i];
			var factoryParameter = factoryParameters[i];

			if (methodParameter.Name != factoryParameter.Name || !Equals(methodParameter.ParameterType, factoryParameter.Type))
			{
				return new(driverType, ValidationErrorCode.RequiredParameterMismatch, methodParameter.ParameterType.Name);
			}
			if (methodParameter.ParameterType.IsByRef)
			{
				return new(driverType, ValidationErrorCode.UnallowedByRefParameter, methodParameter.ParameterType.Name);
			}
		}

		int variableParameterLength = methodParameters.Length - 1;
		for (; i < variableParameterLength; i++)
		{
			var methodParameter = methodParameters[i];

			// If a parameter does not have a matching property, we will check if it is requesting a logger before throwing.
			if (methodParameter.Name is null ||
				!availableParameters.TryGetValue(methodParameter.Name!, out var propertyInfo) && GetLoggerCategory(methodParameter.ParameterType) is not { } loggerCategory)
			{
				return new(driverType, ValidationErrorCode.OptionalParameterNotFound, methodParameter.ParameterType.Name);
			}

			if (propertyInfo is not null && !Equals(methodParameter.ParameterType, propertyInfo.PropertyType))
			{
				if (!methodParameter.ParameterType.IsValueType && GetOptionalBaseType(propertyInfo.PropertyType) is { } optional)
				{
					var optionalArguments = optional.GetGenericArguments();

					if (Equals(methodParameter.ParameterType, optionalArguments[0]))
					{
						continue;
					}
				}
				return new(driverType, ValidationErrorCode.OptionalParameterTypeMismatch, methodParameter.ParameterType.Name);
			}
		}

		return new(driverType, createAsyncMethod, methodParameters);
	}

	private static Delegate CreateFactory
	(
		Type driverType,
		Type delegateType,
		FactoryParameter[] factoryParameters,
		Type contextType,
		Dictionary<string, PropertyInfo> availableParameters,
		Type resultType
	)
	{
		var parseResult = ParseFactoryMethod(driverType, factoryParameters, availableParameters);

		parseResult.ThrowIfFailed();

		int i;

		var parameterTypes = new Type[factoryParameters.Length + 2];
		for (i = 0; i < factoryParameters.Length; i++)
		{
			parameterTypes[i] = factoryParameters[i].Type;
		}
		parameterTypes[i++] = contextType;
		parameterTypes[i] = typeof(CancellationToken);

		var dynamicMethod = new DynamicMethod("CreateAsync", typeof(Task<>).MakeGenericType(resultType), parameterTypes, typeof(DriverFactory));
		var ilGenerator = dynamicMethod.GetILGenerator();

		// Load the context on the stack to prepare for the last call to Complete()
		ilGenerator.Emit(OpCodes.Ldarg, factoryParameters.Length);

		// Handle fixed parameters.
		for (i = 0; i < factoryParameters.Length; i++)
		{
			var factoryParameter = factoryParameters[i];
			ilGenerator.Emit(OpCodes.Ldarg, i);
			dynamicMethod.DefineParameter(i, ParameterAttributes.None, factoryParameter.Name);
		}

		// Handle optional parameters.
		dynamicMethod.DefineParameter(factoryParameters.Length, ParameterAttributes.None, "context");
		int variableParameterLength = parseResult.MethodParameters.Length - 1;
		for (; i < variableParameterLength; i++)
		{
			var methodParameter = parseResult.MethodParameters[i];
			ilGenerator.Emit(OpCodes.Ldarg, factoryParameters.Length);
			if (availableParameters.TryGetValue(methodParameter.Name!, out var propertyInfo))
			{
				ilGenerator.Emit(OpCodes.Callvirt, propertyInfo.GetMethod!);
				// Parameters should already have been checked by the validation method, so if there is a type mismatch, we know we can handle it.
				// For now, we only support unwrapping Optional<T> into T (as this was designed for this), but we may add more cases later if needed.
				if (methodParameter.ParameterType != propertyInfo.PropertyType)
				{
					ilGenerator.EmitCall(OpCodes.Callvirt, GetOptionalBaseType(propertyInfo.PropertyType)!.GetMethod(nameof(Optional<IDisposable>.GetOrCreateValue))!, null);
				}
			}
			else
			{
				// Same remark as above, parameters should already have been checked, so we should be pretty safe about what is available here.
				ilGenerator.Emit(OpCodes.Callvirt, availableParameters[LoggerFactoryParameterName].GetMethod!);
				ilGenerator.Emit(OpCodes.Callvirt, CreateLoggerMethodInfo.MakeGenericMethod(GetLoggerCategory(methodParameter.ParameterType!)!));
			}
		}

		// Handle the last (cancellationToken) parameter.
		ilGenerator.Emit(OpCodes.Ldarg, factoryParameters.Length + 1);
		dynamicMethod.DefineParameter(factoryParameters.Length + 1, ParameterAttributes.None, "cancellationToken");

		// Call the CreateAsync method.
		ilGenerator.EmitCall(OpCodes.Call, parseResult.CreateAsyncMethod, null);

		// Await the method, and downcast it to Task<Driver>. That's a bit stupid, but we have to do this because Task<> cannot be covariant.
		// NB: Keep in mind that having the static CreateAsync method return the exact DriverType is still useful for contract validation.
		ilGenerator.EmitCall(OpCodes.Call, CompleteMethodInfo.MakeGenericMethod(driverType, contextType, resultType), null);

		// And of course, finally return from the method.
		ilGenerator.Emit(OpCodes.Ret);

		return dynamicMethod.CreateDelegate(delegateType);
	}

	/// <summary>Validates the specified driver type for a factory method.</summary>
	/// <remarks>This methods supports working in a MetadataLoadContext.</remarks>
	/// <typeparam name="TFactory"></typeparam>
	/// <typeparam name="TContext"></typeparam>
	/// <typeparam name="TResult"></typeparam>
	/// <param name="driverType">The type of driver to validate.</param>
	/// <returns></returns>
	public static ValidationResult ValidateDriver<TFactory, TContext, TResult>(Type driverType)
		where TFactory : Delegate
		where TContext : class, IDriverCreationContext<TResult>
		where TResult : IDriverCreationResult
	{
		var result = ParseFactoryMethod(driverType, ForContext<TFactory, TContext, TResult>.FactoryParameters, ParameterInformation<TContext, TResult>.Properties);

		return new(result.DriverType, result.ValidationErrorCode, result.ValidationErrorArgument);
	}

	private static async Task<TResult> Complete<TDriver, TContext, TResult>(TContext context, Task<TDriver> task)
		where TDriver : Driver
		where TContext : class, IDriverCreationContext<TResult>
		where TResult : IDriverCreationResult
		=> context.CompleteAndReset(await task.ConfigureAwait(false));

	public static TFactory Get<TFactory, TContext, TResult>(Type type)
		where TFactory : Delegate
		where TContext : class, IDriverCreationContext<TResult>
		where TResult : IDriverCreationResult
		=> ForContext<TFactory, TContext, TResult>.Get(type);

	private static class ParameterInformation<TContext, TResult>
		where TContext : class, IDriverCreationContext<TResult>
		where TResult : IDriverCreationResult
	{
		public static readonly Dictionary<string, PropertyInfo> Properties = ParseContext(typeof(TContext));
	}

	private static class ForContext<TFactory, TContext, TResult>
		where TFactory : Delegate
		where TContext : class, IDriverCreationContext<TResult>
		where TResult : IDriverCreationResult
	{
		public static readonly FactoryParameter[] FactoryParameters = ParseFactoryDelegate(typeof(TFactory), typeof(TContext), typeof(TResult));

		private static readonly ConditionalWeakTable<Type, TFactory> Factories = new();

		public static TFactory Get(Type driverType)
			=> Factories.GetValue(driverType, CreateFactory);

		private static TFactory CreateFactory(Type contextType)
			=> Unsafe.As<TFactory>
			(
				DriverFactory.CreateFactory
				(
					contextType,
					typeof(TFactory),
					FactoryParameters,
					typeof(TContext),
					ParameterInformation<TContext, TResult>.Properties,
					typeof(TResult)
				)
			);
	}
}
