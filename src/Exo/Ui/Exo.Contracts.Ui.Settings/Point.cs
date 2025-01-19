using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly record struct Point
{
	[DataMember(Order = 1)]
	public int X { get; init; }
	[DataMember(Order = 2)]
	public int Y { get; init; }
}
