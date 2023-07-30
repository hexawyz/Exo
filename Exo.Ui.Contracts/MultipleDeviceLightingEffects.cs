using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MultipleDeviceLightingEffects
{
	[DataMember(Order = 1)]
	public required ImmutableArray<DeviceLightingEffects> DeviceEffects { get; init; }
}
