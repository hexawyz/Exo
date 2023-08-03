namespace Exo;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class TypeIdAttribute : Attribute
{
	public TypeIdAttribute(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
		=> Value = new(a, b, c, d, e, f, g, h, i, j, k);

	public Guid Value { get; }
}
