using System.Collections.Immutable;
using Exo.Lighting;

namespace Exo.Service;

public readonly struct LightingDeviceInformation : IEquatable<LightingDeviceInformation>
{
	public Guid DeviceId { get; }
	public LightingPersistenceMode PersistenceMode { get; }
	public BrightnessCapabilities? BrightnessCapabilities { get; }
	public PaletteCapabilities? PaletteCapabilities { get; }
	public LightingZoneInformation? UnifiedLightingZone { get; }
	public ImmutableArray<LightingZoneInformation> LightingZones { get; }

	public LightingDeviceInformation
	(
		Guid deviceId,
		LightingPersistenceMode persistenceMode,
		BrightnessCapabilities? brightnessCapabilities,
		PaletteCapabilities? paletteCapabilities,
		LightingZoneInformation? unifiedLightingZone,
		ImmutableArray<LightingZoneInformation> lightingZones
	)
	{
		DeviceId = deviceId;
		PersistenceMode = persistenceMode;
		BrightnessCapabilities = brightnessCapabilities;
		PaletteCapabilities = paletteCapabilities;
		UnifiedLightingZone = unifiedLightingZone;
		LightingZones = lightingZones;
	}

	public override bool Equals(object? obj) => obj is LightingDeviceInformation information && Equals(information);

	public bool Equals(LightingDeviceInformation other)
		=> DeviceId == other.DeviceId &&
			BrightnessCapabilities == other.BrightnessCapabilities &&
			PaletteCapabilities == other.PaletteCapabilities &&
			EqualityComparer<LightingZoneInformation?>.Default.Equals(UnifiedLightingZone, other.UnifiedLightingZone) &&
			LightingZones.SequenceEqual(other.LightingZones);

	public override int GetHashCode() => HashCode.Combine(DeviceId, BrightnessCapabilities, PaletteCapabilities, UnifiedLightingZone, LightingZones.IsDefaultOrEmpty ? 0 : LightingZones.Length);

	public static bool operator ==(LightingDeviceInformation left, LightingDeviceInformation right) => left.Equals(right);
	public static bool operator !=(LightingDeviceInformation left, LightingDeviceInformation right) => !(left == right);
}
