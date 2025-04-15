using System.Collections.Immutable;
using Exo.ColorFormats;

namespace Exo.Service;

public readonly struct LightingDeviceConfigurationUpdate(Guid deviceId, bool shouldPersist, byte? brightnessLevel, ImmutableArray<RgbColor> paletteColors, ImmutableArray<LightingZoneEffect> zoneEffects)
{
	public Guid DeviceId { get; } = deviceId;

	public bool ShouldPersist { get; } = shouldPersist;

	public byte? BrightnessLevel { get; } = brightnessLevel;

	public ImmutableArray<RgbColor> PaletteColors { get; } = paletteColors;

	public ImmutableArray<LightingZoneEffect> ZoneEffects { get; } = zoneEffects;
}
