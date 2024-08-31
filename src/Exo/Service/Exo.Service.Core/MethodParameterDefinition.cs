using System.Reflection;

namespace Exo.Service;

internal readonly record struct MethodParameterDefinition(string? ParameterName, TypeReference ParameterType, bool IsByRef)
{
	public MethodParameterDefinition(ParameterInfo parameterInfo) : this(parameterInfo.Name!, parameterInfo.ParameterType, parameterInfo.ParameterType.IsByRef) { }

	public static implicit operator MethodParameterDefinition(ParameterInfo parameter) => new(parameter);
}
