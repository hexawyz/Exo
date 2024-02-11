namespace Exo.Service;

internal readonly record struct TypeReference(string AssemblyName, string TypeName)
{
	public static implicit operator TypeReference(Type type)
		=> new(type.Assembly.FullName!, type.FullName!);

	public bool Matches(Type type) => AssemblyName == type.Assembly.FullName && type.FullName == TypeName;
}
