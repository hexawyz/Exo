using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct LightingDeviceInformation
{
	public LightingZoneInformation? UnifiedLightingZone { get; }
	public ImmutableArray<LightingZoneInformation> LightingZones { get; }

	public LightingDeviceInformation(LightingZoneInformation? unifiedLightingZone, ImmutableArray<LightingZoneInformation> lightingZones)
	{
		UnifiedLightingZone = unifiedLightingZone;
		LightingZones = lightingZones;
	}
}
