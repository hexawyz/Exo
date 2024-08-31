namespace Exo.Programming.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleAttribute : Attribute
{
	public string Name { get; }

	public ModuleAttribute(string name) => Name = name;
}
