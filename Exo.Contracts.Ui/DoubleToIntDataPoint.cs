using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class DoubleToIntDataPoint
{
	[DataMember(Order = 1)]
	public double X { get; init; }
	[DataMember(Order = 2)]
	public long Y { get; init; }
}
