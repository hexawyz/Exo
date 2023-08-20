using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MultiDeviceLightingUpdates
{
	[DataMember(Order = 1)]
	public required ImmutableArray<DeviceLightingUpdate> DeviceEffects { get; init; }
}
