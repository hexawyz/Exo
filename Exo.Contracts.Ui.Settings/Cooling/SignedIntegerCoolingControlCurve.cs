using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class SignedIntegerCoolingControlCurve
{
	[DataMember(Order = 1)]
	public required byte InitialValue { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<IntToUIntDataPoint> SegmentPoints { get; init; }
}
