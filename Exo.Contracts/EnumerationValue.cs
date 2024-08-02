using System.Runtime.Serialization;

namespace Exo.Contracts;

[DataContract]
public readonly record struct EnumerationValue
{
	/// <summary>The underlying value of the enumeration item.</summary>
	[DataMember(Order = 1)]
	public required ulong Value { get; init; }
	/// <summary>The display name associated with the enumeration item.</summary>
	[DataMember(Order = 2)]
	public required string DisplayName { get; init; }
	/// <summary>The display name associated with the enumeration item.</summary>
	[DataMember(Order = 3)]
	public string? Description { get; init; }
}
