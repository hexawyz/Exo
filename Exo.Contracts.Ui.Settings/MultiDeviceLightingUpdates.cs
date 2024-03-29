using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MultiDeviceLightingUpdates
{
	[DataMember(Order = 1)]
	public required ImmutableArray<DeviceLightingUpdate> DeviceUpdates { get; init; }
}
