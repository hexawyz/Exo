using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class LightingDeviceInformation
{
	[DataMember(Order = 1)]
	public required DeviceInformation DeviceInformation { get; init; }
	[DataMember(Order = 2)]
	public LightingZoneInformation? UnifiedLightingZone { get; init; }
	[DataMember(Order = 3)]
	public required ImmutableArray<LightingZoneInformation> LightingZones { get; init; }
}
