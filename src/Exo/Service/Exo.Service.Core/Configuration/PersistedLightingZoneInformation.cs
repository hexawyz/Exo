using System.Collections.Immutable;

namespace Exo.Service.Configuration;

[TypeId(0xB6677089, 0x77FE, 0x467A, 0x8C, 0x23, 0x87, 0x8C, 0x80, 0x71, 0x03, 0x19)]
internal readonly struct PersistedLightingZoneInformation
{
	public PersistedLightingZoneInformation(LightingZoneInformation info)
	{
		SupportedEffectTypeIds = info.SupportedEffectTypeIds;
	}

	public ImmutableArray<Guid> SupportedEffectTypeIds { get; init; }
}
