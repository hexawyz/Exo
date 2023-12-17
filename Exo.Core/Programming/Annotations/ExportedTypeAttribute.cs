namespace Exo.Programming.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ExportedTypeAttribute : Attribute
{
	public ExportedTypeAttribute(Type type) => Type = type;

	public Type Type { get; }
}
