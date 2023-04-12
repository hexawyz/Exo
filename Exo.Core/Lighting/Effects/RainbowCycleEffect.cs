namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors will cycle through the color wheel.</summary>
public readonly struct RainbowCycleEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new RainbowCycleEffect();
}
