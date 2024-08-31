using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly struct NonContinuousValue
{
	[DataMember(Order = 1)]
	public required ushort Value { get; init; }
	[DataMember(Order = 2)]
	public Guid? NameStringId { get; init; }
	[DataMember(Order = 3)]
	public string? CustomName { get; init; }
}
