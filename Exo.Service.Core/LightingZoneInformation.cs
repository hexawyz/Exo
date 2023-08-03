using System;
using System.Collections.Immutable;

namespace Exo.Service;

// Making this is a class should be more efficient to reference it from other parts of the code.
// Lighting zone information objects usually be should be long-lived, so it is not an unwise choice to make.
public sealed class LightingZoneInformation
{
	public LightingZoneInformation(Guid zoneId, ImmutableArray<Type> supportedEffectTypes)
	{
		ZoneId = zoneId;
		SupportedEffectTypes = supportedEffectTypes;
	}

	public Guid ZoneId { get; }
	public ImmutableArray<Type> SupportedEffectTypes { get; }
}
