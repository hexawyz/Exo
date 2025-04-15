using System.Collections.Immutable;

namespace Exo.Service;

[TypeId(0xB6677089, 0x77FE, 0x467A, 0x8C, 0x23, 0x87, 0x8C, 0x80, 0x71, 0x03, 0x19)]
public readonly struct LightingZoneInformation : IEquatable<LightingZoneInformation>
{
	public LightingZoneInformation(Guid zoneId, ImmutableArray<Guid> supportedEffectTypeIds)
	{
		ZoneId = zoneId;
		SupportedEffectTypeIds = supportedEffectTypeIds;
	}

	public Guid ZoneId { get; }
	public ImmutableArray<Guid> SupportedEffectTypeIds { get; }

	public override bool Equals(object? obj) => obj is LightingZoneInformation info && Equals(info);

	public bool Equals(LightingZoneInformation other)
		=> ZoneId.Equals(other.ZoneId) &&
			SupportedEffectTypeIds.SequenceEqual(other.SupportedEffectTypeIds);

	public override int GetHashCode() => HashCode.Combine(ZoneId, SupportedEffectTypeIds.IsDefaultOrEmpty ? 0 : SupportedEffectTypeIds.Length);

	public static bool operator ==(LightingZoneInformation left, LightingZoneInformation right) => EqualityComparer<LightingZoneInformation>.Default.Equals(left, right);
	public static bool operator !=(LightingZoneInformation left, LightingZoneInformation right) => !(left == right);
}
