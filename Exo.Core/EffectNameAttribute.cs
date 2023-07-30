namespace Exo;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EffectNameAttribute : Attribute
{
	public EffectNameAttribute(string name) => Name = name;

	public string Name { get; }
}
