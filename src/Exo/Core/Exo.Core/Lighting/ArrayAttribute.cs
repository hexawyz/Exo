namespace Exo.Lighting;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ArrayAttribute(int minimumLength, int maximumLength) : Attribute
{
	public int MinimumLength { get; } = minimumLength;
	public int MaximumLength { get; } = maximumLength;
}
