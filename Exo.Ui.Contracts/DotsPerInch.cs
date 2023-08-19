using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public readonly struct DotsPerInch
{
	[DataMember(Order = 1)]
	public required ushort Horizontal { get; init; }

	[DataMember(Order = 2)]
	public required ushort Vertical { get; init; }
}
