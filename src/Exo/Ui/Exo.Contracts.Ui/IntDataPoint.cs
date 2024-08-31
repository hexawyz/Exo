using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class IntDataPoint
{
	[DataMember(Order = 1)]
	public long X { get; init; }
	[DataMember(Order = 2)]
	public long Y { get; init; }
}
