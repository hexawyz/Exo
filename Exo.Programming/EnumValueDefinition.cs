using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EnumValueDefinition
{
	[DataMember(Order = 1)]
	public required string Name { get; init; }
	[DataMember(Order = 2)]
	public long Value { get; init; }
	[DataMember(Order = 3)]
	public string? Comment { get; init; }
}
