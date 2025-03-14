using System.Diagnostics;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
[DebuggerDisplay("X: {X}, Y: {Y}")]
public sealed class SingleToUIntDataPoint : IDataPoint<float, ulong>
{
	[DataMember(Order = 1)]
	public float X { get; init; }
	[DataMember(Order = 2)]
	public ulong Y { get; init; }
}
