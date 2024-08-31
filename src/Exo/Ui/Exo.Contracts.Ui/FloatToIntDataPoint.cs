using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class FloatToIntDataPoint
{
	[DataMember(Order = 1)]
	public float X { get; init; }
	[DataMember(Order = 2)]
	public long Y { get; init; }
}
