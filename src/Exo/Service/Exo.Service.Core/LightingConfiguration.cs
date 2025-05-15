using Exo.Lighting;

namespace Exo.Service;

public readonly struct LightingConfiguration(bool useCentralizedLightingEnabled, LightingEffect? centralizedLightingEffect)
{
	/// <summary>Gets a value indicating if centralized lighting is enabled.</summary>
	public bool UseCentralizedLighting { get; } = useCentralizedLightingEnabled;
	/// <summary>Gets a value indicating the current centralized lighting effect, if changed.</summary>
	public LightingEffect? CentralizedLightingEffect { get; } = centralizedLightingEffect;
}
