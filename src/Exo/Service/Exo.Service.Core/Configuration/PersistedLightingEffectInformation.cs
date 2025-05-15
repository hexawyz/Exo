using System.Collections.Immutable;
using Exo.Lighting;

namespace Exo.Service.Configuration;

[TypeId(0x3B7410BA, 0xF28E, 0x498E, 0xB7, 0x23, 0x4A, 0xE9, 0x09, 0xDF, 0xBA, 0xFC)]
public readonly struct PersistedLightingEffectInformation
{
	public required ImmutableArray<ConfigurablePropertyInformation> Properties { get; init; }
}
