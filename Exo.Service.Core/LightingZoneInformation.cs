using System;
using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct LightingZoneInformation
{
	public LightingZoneInformation(Guid zoneId, ImmutableArray<Type> supportedEffectTypes)
	{
		ZoneId = zoneId;
		SupportedEffectTypes = supportedEffectTypes;
	}

	public Guid ZoneId { get; }
	public ImmutableArray<Type> SupportedEffectTypes { get; }
}
