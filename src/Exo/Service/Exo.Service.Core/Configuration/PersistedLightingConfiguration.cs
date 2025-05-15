using Exo.Lighting;

namespace Exo.Service.Configuration;

[TypeId(0xE92CEB83, 0x8A19, 0x4A1A, 0x94, 0xFA, 0x42, 0xCC, 0x6D, 0x4F, 0x40, 0x89)]
internal readonly struct PersistedLightingConfiguration
{
	public bool IsCentralizedLightingEnabled { get; init; }
	public LightingEffect CentralizedLightingEffect { get; init; }
}
