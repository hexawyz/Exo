using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct LightingDeviceInformation : IEquatable<LightingDeviceInformation>
{
	public LightingZoneInformation? UnifiedLightingZone { get; }
	public ImmutableArray<LightingZoneInformation> LightingZones { get; }
	public bool HasBrightness { get; }

	public LightingDeviceInformation(LightingZoneInformation? unifiedLightingZone, ImmutableArray<LightingZoneInformation> lightingZones, bool hasBrightness)
	{
		UnifiedLightingZone = unifiedLightingZone;
		LightingZones = lightingZones;
		HasBrightness = hasBrightness;
	}

	public override bool Equals(object? obj) => obj is LightingDeviceInformation information && Equals(information);

	public bool Equals(LightingDeviceInformation other)
		=> HasBrightness == other.HasBrightness &&
			EqualityComparer<LightingZoneInformation?>.Default.Equals(UnifiedLightingZone, other.UnifiedLightingZone) &&
			LightingZones.SequenceEqual(other.LightingZones);

	public override int GetHashCode() => HashCode.Combine(HasBrightness, UnifiedLightingZone, LightingZones.Length());

	public static bool operator ==(LightingDeviceInformation left, LightingDeviceInformation right) => left.Equals(right);
	public static bool operator !=(LightingDeviceInformation left, LightingDeviceInformation right) => !(left == right);
}
