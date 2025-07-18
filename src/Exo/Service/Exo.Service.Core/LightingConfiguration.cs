using Exo.Lighting;

namespace Exo.Service;

public readonly struct LightingConfiguration(bool useCentralizedLighting, LightingEffect? centralizedLightingEffect)
{
	/// <summary>Gets a value indicating if centralized lighting is enabled.</summary>
	public bool UseCentralizedLighting { get; } = useCentralizedLighting;
	/// <summary>Gets a value indicating the current centralized lighting effect, if changed.</summary>
	public LightingEffect? CentralizedLightingEffect { get; } = centralizedLightingEffect;
}
