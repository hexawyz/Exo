using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightingDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public LightingBrightnessCapabilities? BrightnessCapabilities { get; init; }
	[DataMember(Order = 3)]
	public LightingPaletteCapabilities? PaletteCapabilities { get; init; }
	[DataMember(Order = 4)]
	public LightingZoneInformation? UnifiedLightingZone { get; init; }
	[DataMember(Order = 5)]
	public required ImmutableArray<LightingZoneInformation> LightingZones { get; init; }
}
