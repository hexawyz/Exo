using Exo.Contracts;

namespace Exo.Service;

public readonly struct LightingZoneEffect(Guid zoneId, LightingEffect? effect)
{
	public Guid ZoneId { get; } = zoneId;
	public LightingEffect? Effect { get; } = effect;
}
