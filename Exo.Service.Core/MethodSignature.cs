using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Service;

internal readonly struct MethodSignature : IEquatable<MethodSignature>
{
	public string MethodName { get; init; }
	public TypeReference ReturnType { get; init; }
	public ImmutableArray<MethodParameterDefinition> Parameters { get; init; }

	public MethodSignature(string methodName, TypeReference returnType, ImmutableArray<MethodParameterDefinition> parameters)
	{
		if (parameters.IsDefaultOrEmpty) throw new ArgumentNullException(nameof(parameters));

		MethodName = methodName;
		ReturnType = returnType;
		Parameters = parameters;
	}

	public MethodSignature(MethodInfo method)
	{
		MethodName = method.Name;
		ReturnType = method.ReturnType;
		var otherParameters = method.GetParameters();
		if (otherParameters.Length != 0)
		{
			var parameters = new MethodParameterDefinition[otherParameters.Length];
			for (int i = 0; i < otherParameters.Length; i++)
			{
				parameters[i] = otherParameters[i];
			}
			Parameters = ImmutableCollectionsMarshal.AsImmutableArray(parameters);
		}
		else
		{
			Parameters = [];
		}
	}

	public bool Matches(MethodInfo method)
	{
		if (method.Name == MethodName && ReturnType.Matches(method.ReturnType))
		{
			var otherParameters = method.GetParameters();

			if (Parameters.Length == otherParameters.Length)
			{
				for (int i = 0; i < Parameters.Length; i++)
				{
					var parameter = Parameters[i];
					var otherParameter = otherParameters[i];

					if (parameter.ParameterName != otherParameter.Name || parameter.IsByRef != otherParameter.ParameterType.IsByRef || !parameter.ParameterType.Matches(otherParameter.ParameterType))
					{
						goto DoesNotMatch;
					}
				}
				return true;
			}
		}
	DoesNotMatch:;
		return false;
	}

	public override string ToString()
	{
		if (Parameters.IsDefaultOrEmpty) return "";

		var sb = new StringBuilder(100);
		sb.Append(ReturnType.TypeName)
			.Append(' ')
			.Append(MethodName)
			.Append('(');

		for (int i = 0; i < Parameters.Length; i++)
		{
			var parameter = Parameters[i];
			if (i > 0)
			{
				sb.Append(", ");
			}
			sb.Append(parameter.ParameterType.TypeName);
			(parameter.IsByRef ? sb.Append("& ") : sb.Append(' ')).Append(parameter.ParameterName);
		}

		sb.Append(')');

		return sb.ToString();
	}

	public override bool Equals(object? obj) => obj is MethodSignature reference && Equals(reference);
	public bool Equals(MethodSignature other) => MethodName == other.MethodName && ReturnType.Equals(other.ReturnType) && Parameters.SequenceEqual(other.Parameters);
	public override int GetHashCode() => HashCode.Combine(MethodName, ReturnType, Parameters.Length);

	public static bool operator ==(MethodSignature left, MethodSignature right) => left.Equals(right);
	public static bool operator !=(MethodSignature left, MethodSignature right) => !(left == right);

	public static implicit operator MethodSignature(MethodInfo method) => new(method);
}
