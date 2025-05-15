using System.Collections.Immutable;
using Exo.ColorFormats;
using Exo.Lighting;

namespace Exo.Service;

public readonly struct LightingDeviceConfiguration(Guid deviceId, bool isUnifiedLightingEnabled, byte? brightnessLevel, ImmutableArray<RgbColor> paletteColors, ImmutableArray<LightingZoneEffect> zoneEffects)
{
	/// <summary>Gets the ID of the device.</summary>
	public Guid DeviceId { get; } = deviceId;

	/// <summary>Gets a value indicating if unified lighting is enabled on the device.</summary>
	public bool IsUnifiedLightingEnabled { get; } = isUnifiedLightingEnabled;

	/// <summary>Gets the default brightness level of the device.</summary>
	/// <remarks>
	/// The brightness is expressed in device-specific units.
	/// It corresponds to the default brightness the device will apply to all effects excepts effect overriding the brightness if the device supports it.
	/// </remarks>
	public byte? BrightnessLevel { get; } = brightnessLevel;

	public ImmutableArray<RgbColor> PaletteColors { get; } = paletteColors;

	public ImmutableArray<LightingZoneEffect> ZoneEffects { get; } = zoneEffects;
}
