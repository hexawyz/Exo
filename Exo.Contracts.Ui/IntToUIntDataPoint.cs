using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class IntToUIntDataPoint
{
	[DataMember(Order = 1)]
	public long X { get; init; }
	[DataMember(Order = 2)]
	public ulong Y { get; init; }
}
