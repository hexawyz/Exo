using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Exo.Discovery;
using Microsoft.Extensions.Logging;

namespace Exo;

/// <summary>Helpers to create factory methods for components.</summary>
/// <remarks>
/// Usage of this class is recommended to provide efficient access to component factory methods.
/// It will do all the necessary parsing and validation, and allow injecting optional parameters into the Component.CreateAsync method.
/// </remarks>
public static class ComponentFactory
{
	public enum ValidationErrorCode
	{
		None = 0,
		InvalidReturnType = 1,
		InvalidCancellationToken = 2,
		RequiredParameterMismatch = 3,
		UnallowedByRefParameter = 4,
		OptionalParameterNotFound = 5,
		OptionalParameterTypeMismatch = 6,
	}

	public readonly struct ValidationResult
	{
		public ValidationResult(MethodInfo method, ValidationErrorCode errorCode, string? argument)
		{
			Method = method;
			ErrorCode = errorCode;
			Argument = argument;
		}

		public MethodInfo Method { get; }
		public ValidationErrorCode ErrorCode { get; }
		public string? Argument { get; }

		public void ThrowIfFailed()
		{
			if (ErrorCode != ValidationErrorCode.None)
			{
				throw new InvalidOperationException(ErrorCode.FormatError(Method, Argument));
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
		public FactoryMethodParseResult(MethodInfo method, ParameterInfo[] parameters)
			: this(method, parameters, ValidationErrorCode.None, null)
		{
		}

		public FactoryMethodParseResult(MethodInfo method, ValidationErrorCode validationErrorCode, string? validationErrorArgument)
			: this(method, null, validationErrorCode, validationErrorArgument)
		{
		}

		private FactoryMethodParseResult(MethodInfo? method, ParameterInfo[]? parameters, ValidationErrorCode validationErrorCode, string? validationErrorArgument)
		{
			Method = method;
			MethodParameters = parameters;
			ValidationErrorCode = validationErrorCode;
			ValidationErrorArgument = validationErrorArgument;
		}

		public MethodInfo? Method { get; }
		public ParameterInfo[]? MethodParameters { get; }
		public ValidationErrorCode ValidationErrorCode { get; }
		public string? ValidationErrorArgument { get; }

		[MemberNotNull(nameof(Method))]
		[MemberNotNull(nameof(MethodParameters))]
		public void ThrowIfFailed()
		{
			if (ValidationErrorCode != ValidationErrorCode.None)
			{
				throw new InvalidOperationException(ValidationErrorCode.FormatError(Method, ValidationErrorArgument));
			}
			else if (Method is null || MethodParameters is null)
			{
				throw new InvalidOperationException();
			}
		}
	}

	private static readonly MethodInfo CreateLoggerMethodInfo =
		typeof(LoggerFactoryExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == nameof(LoggerFactoryExtensions.CreateLogger) && m.GetParameters().Length == 1 && m.IsGenericMethod && m.GetGenericArguments().Length == 1);

	private static readonly string LoggerFactoryParameterName = Naming.MakeCamelCase(nameof(IComponentCreationContext.LoggerFactory));

	private static Type? GetOptionalBaseType(Type type)
	{
		if (type.IsValueType) return null;

		Type? current = type;
		while (current is not null && current != typeof(object))
		{
			if (current.IsGenericType && current.GetGenericTypeDefinition().Matches(typeof(Optional<>)))
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

		if (type.IsGenericType && type.GetGenericTypeDefinition().Matches(typeof(ILogger<>)))
		{
			return type.GetGenericArguments()[0];
		}

		return null;
	}

	private static Dictionary<string, PropertyInfo> ParseContext(Type type)
	{
		var allowedProperties = new Dictionary<string, PropertyInfo>();
		foreach (var property in type.GetProperties())
		{
			allowedProperties.Add(Naming.MakeCamelCase(property.Name), property);
		}
		if (!(allowedProperties.TryGetValue(LoggerFactoryParameterName, out var loggerFactoryProperty) && loggerFactoryProperty.PropertyType.Matches<ILoggerFactory>()))
		{
			throw new ArgumentException("Component creation context must expose the LoggerFactoryProperty of type ILoggerFactory.");
		}
		return allowedProperties;
	}

	private static FactoryParameter[] ParseFactoryDelegate(Type delegateType, Type contextType, Type resultType)
	{
		var invokeMethod = delegateType.GetMethod("Invoke") ?? throw new InvalidOperationException($"The type {delegateType} is nto a valid delegate type.");
		var parameters = invokeMethod.GetParameters();
		if (!invokeMethod.ReturnType.Matches(typeof(ValueTask<>).MakeGenericType(resultType)) ||
			parameters.Length < 2 ||
			parameters[^1] is not { Name: "cancellationToken", ParameterType.IsByRef: false } ctParameter ||
			!ctParameter.ParameterType.Matches<CancellationToken>() ||
			parameters[^2] is not { Name: "context", ParameterType.IsByRef: false } pParameter ||
			!pParameter.ParameterType.Matches(contextType))
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
	private static FactoryMethodParseResult ParseFactoryMethod(MethodInfo method, Type resultType, FactoryParameter[] factoryParameters, Dictionary<string, PropertyInfo> availableParameters)
	{
		ArgumentNullException.ThrowIfNull(method);

		if (method.ReturnType.IsByRef ||
			!method.ReturnType.IsGenericType ||
			method.ReturnType.GetGenericTypeDefinition() is var genericReturnType && !(genericReturnType.Matches(typeof(ValueTask<>)) || genericReturnType.Matches(typeof(Task<>))) ||
			method.ReturnType.GetGenericArguments() is not { } taskParameters ||
			!taskParameters[0].Matches(resultType))
		{
			return new(method, ValidationErrorCode.InvalidReturnType, null);
		}

		var methodParameters = method.GetParameters();

		// At the minimum, the CreateAsync method must include all the required factory parameters, plus a cancellation token parameter.
		if (methodParameters.Length < factoryParameters.Length + 1) throw new InvalidOperationException($"The method {method} does not have enough parameters.");

		// The CancellationToken parameter will be optional, but if present, it must be last and named "cancellationToken".
		var lastParameter = methodParameters[^1];
		bool hasCancellationToken = lastParameter is { Name: "cancellationToken", ParameterType.IsByRef: false };
		if (hasCancellationToken && !lastParameter.ParameterType.Matches<CancellationToken>())
		{
			return new(method, ValidationErrorCode.InvalidCancellationToken, null);
		}

		int i = 0;
		for (; i < factoryParameters.Length; i++)
		{
			var methodParameter = methodParameters[i];
			var factoryParameter = factoryParameters[i];

			if (methodParameter.Name != factoryParameter.Name || !Equals(methodParameter.ParameterType, factoryParameter.Type))
			{
				return new(method, ValidationErrorCode.RequiredParameterMismatch, methodParameter.Name);
			}
			if (methodParameter.ParameterType.IsByRef)
			{
				return new(method, ValidationErrorCode.UnallowedByRefParameter, methodParameter.Name);
			}
		}

		int variableParameterLength = methodParameters.Length - (hasCancellationToken ? 1 : 0);
		for (; i < variableParameterLength; i++)
		{
			var methodParameter = methodParameters[i];

			// If a parameter does not have a matching property, we will check if it is requesting a logger before throwing.
			if (methodParameter.Name is null ||
				!availableParameters.TryGetValue(methodParameter.Name!, out var propertyInfo) && GetLoggerCategory(methodParameter.ParameterType) is not { } loggerCategory)
			{
				return new(method, ValidationErrorCode.OptionalParameterNotFound, methodParameter.Name);
			}

			if (propertyInfo is not null && !methodParameter.ParameterType.Matches(propertyInfo.PropertyType))
			{
				if (!methodParameter.ParameterType.IsValueType && GetOptionalBaseType(propertyInfo.PropertyType) is { } optional)
				{
					var optionalArguments = optional.GetGenericArguments();

					if (methodParameter.ParameterType.Matches(optionalArguments[0]))
					{
						continue;
					}
				}
				return new(method, ValidationErrorCode.OptionalParameterTypeMismatch, methodParameter.Name);
			}
		}

		return new(method, methodParameters);
	}

	private static Delegate CreateFactory
	(
		MethodInfo method,
		Type delegateType,
		FactoryParameter[] factoryParameters,
		Type contextType,
		Dictionary<string, PropertyInfo> availableParameters,
		Type resultType
	)
	{
		var parseResult = ParseFactoryMethod(method, resultType, factoryParameters, availableParameters);

		parseResult.ThrowIfFailed();

		int i;

		var parameterTypes = new Type[factoryParameters.Length + 2];
		for (i = 0; i < factoryParameters.Length; i++)
		{
			parameterTypes[i] = factoryParameters[i].Type;
		}
		parameterTypes[i++] = contextType;
		parameterTypes[i] = typeof(CancellationToken);

		var dynamicMethod = new DynamicMethod("CreateAsync", typeof(ValueTask<>).MakeGenericType(resultType), parameterTypes, typeof(ComponentFactory));
		var ilGenerator = dynamicMethod.GetILGenerator();

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
		var lastParameter = parseResult.MethodParameters[variableParameterLength];
		bool hasCancellationToken = lastParameter.Name == "cancellationToken" && lastParameter.ParameterType == typeof(CancellationToken);
		if (!hasCancellationToken) variableParameterLength++;
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
				ilGenerator.Emit(OpCodes.Call, CreateLoggerMethodInfo.MakeGenericMethod(GetLoggerCategory(methodParameter.ParameterType!)!));
			}
		}

		// Handle the final cancellationToken parameter.
		if (hasCancellationToken) ilGenerator.Emit(OpCodes.Ldarg, factoryParameters.Length + 1);
		dynamicMethod.DefineParameter(factoryParameters.Length + 1, ParameterAttributes.None, "cancellationToken");

		// Call the CreateAsync method.
		ilGenerator.EmitCall(OpCodes.Call, parseResult.Method, null);

		// If necessary, wrap the result in ValueTask.
		if (parseResult.Method.ReturnType.GetGenericTypeDefinition().Matches(typeof(Task<>)))
		{
			var genericArgumentTypes = parseResult.Method.ReturnType.GetGenericArguments();
			ilGenerator.Emit(OpCodes.Newobj, typeof(ValueTask<>).MakeGenericType(genericArgumentTypes).GetConstructor([typeof(Task<>).MakeGenericType(genericArgumentTypes)])!);
		}

		// And of course, finally return from the method.
		ilGenerator.Emit(OpCodes.Ret);

		return dynamicMethod.CreateDelegate(delegateType);
	}

	/// <summary>Validates the specified method as a factory for the associated context and result.</summary>
	/// <remarks>This methods supports working in a MetadataLoadContext.</remarks>
	/// <typeparam name="TFactory">The type of factory expected for the method.</typeparam>
	/// <typeparam name="TContext">The type of context that can provide parameters to the method.</typeparam>
	/// <typeparam name="TResult">The expected result type of the factory.</typeparam>
	/// <param name="method">The method to validate as a factory.</param>
	/// <returns></returns>
	public static ValidationResult Validate<TFactory, TContext, TResult>(MethodInfo method)
		where TFactory : Delegate
		where TContext : class, IComponentCreationContext
	{
		var result = ParseFactoryMethod(method, typeof(TResult), ForContext<TFactory, TContext, TResult>.FactoryParameters, ParameterInformation<TContext>.Properties);

		return new(method, result.ValidationErrorCode, result.ValidationErrorArgument);
	}

	/// <summary>Validates the specified factory method for the proper context and result.</summary>
	/// <remarks>This methods supports working in a MetadataLoadContext.</remarks>
	/// <typeparam name="TFactory"></typeparam>
	/// <typeparam name="TContext"></typeparam>
	/// <typeparam name="TResult"></typeparam>
	/// <param name="method">The method to validate as a factory.</param>
	/// <param name="factoryType">The type of factory expected for the method.</param>
	/// <param name="contextType">The type of context that can provide parameters to the method.</param>
	/// <param name="resultType">The expected result type of the factory.</param>
	/// <returns></returns>
	public static ValidationResult Validate(MethodInfo method, Type factoryType, Type contextType, Type resultType)
	{
		var result = ParseFactoryMethod(method, resultType, ParseFactoryDelegate(factoryType, contextType, resultType), ParseContext(contextType));

		return new(method, result.ValidationErrorCode, result.ValidationErrorArgument);
	}

	public static TFactory Get<TFactory, TContext, TResult>(MethodInfo method)
		where TFactory : Delegate
		where TContext : class, IComponentCreationContext
		=> ForContext<TFactory, TContext, TResult>.Get(method);

	private static class ParameterInformation<TContext>
		where TContext : class, IComponentCreationContext
	{
		public static readonly Dictionary<string, PropertyInfo> Properties = ParseContext(typeof(TContext));
	}

	private static class ForContext<TFactory, TContext, TResult>
		where TFactory : Delegate
		where TContext : class, IComponentCreationContext
	{
		public static readonly FactoryParameter[] FactoryParameters = ParseFactoryDelegate(typeof(TFactory), typeof(TContext), typeof(TResult));

		private static readonly ConditionalWeakTable<MethodInfo, TFactory> Factories = new();

		public static TFactory Get(MethodInfo method)
			=> Factories.GetValue(method, CreateFactory);

		private static TFactory CreateFactory(MethodInfo method)
			=> Unsafe.As<TFactory>
			(
				ComponentFactory.CreateFactory
				(
					method,
					typeof(TFactory),
					FactoryParameters,
					typeof(TContext),
					ParameterInformation<TContext>.Properties,
					typeof(TResult)
				)
			);
	}
}
