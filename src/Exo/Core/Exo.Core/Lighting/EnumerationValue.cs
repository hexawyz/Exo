namespace Exo.Lighting;

public readonly record struct EnumerationValue
{
	/// <summary>The underlying value of the enumeration item.</summary>
	public required ulong Value { get; init; }
	/// <summary>The display name associated with the enumeration item.</summary>
	public required string DisplayName { get; init; }
	/// <summary>The display name associated with the enumeration item.</summary>
	public string? Description { get; init; }
}
