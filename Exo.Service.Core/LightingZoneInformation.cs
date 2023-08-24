using System.Collections.Immutable;

namespace Exo.Service;

// Making this is a class should be more efficient to reference it from other parts of the code.
// Lighting zone information objects usually be should be long-lived, so it is not an unwise choice to make.
public sealed class LightingZoneInformation : IEquatable<LightingZoneInformation?>
{
	public LightingZoneInformation(Guid zoneId, ImmutableArray<Type> supportedEffectTypes)
	{
		ZoneId = zoneId;
		SupportedEffectTypes = supportedEffectTypes;
	}

	public Guid ZoneId { get; }
	public ImmutableArray<Type> SupportedEffectTypes { get; }

	public override bool Equals(object? obj) => Equals(obj as LightingZoneInformation);

	public bool Equals(LightingZoneInformation? other)
		=> other is not null && ZoneId.Equals(other.ZoneId) &&
			SupportedEffectTypes.SequenceEqual(other.SupportedEffectTypes);

	public override int GetHashCode() => HashCode.Combine(ZoneId, SupportedEffectTypes.Length());

	public static bool operator ==(LightingZoneInformation? left, LightingZoneInformation? right) => EqualityComparer<LightingZoneInformation>.Default.Equals(left, right);
	public static bool operator !=(LightingZoneInformation? left, LightingZoneInformation? right) => !(left == right);
}
