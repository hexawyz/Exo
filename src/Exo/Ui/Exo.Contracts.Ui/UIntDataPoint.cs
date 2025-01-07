using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class UIntDataPoint : IDataPoint<ulong, ulong>
{
	[DataMember(Order = 1)]
	public ulong X { get; init; }
	[DataMember(Order = 2)]
	public ulong Y { get; init; }
}
