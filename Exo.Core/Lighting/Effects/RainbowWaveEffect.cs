namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors of the rainbow will move in a wave.</summary>
/// <remarks>
/// <para>
/// This is a slightly more advanced version of <see cref="RainbowCycleEffect"/> that is designed for adressable lighting zones only.
/// The adressable lighting zones supporting this effect may not necessarily be adressable through software, but the effect will work as long as the lighting controller supports it.
/// </para>
/// <para>
/// Some lighting controllers may support applying this effect across multiple zones, as if they were a single addressable lighting zone.
/// </para>
/// </remarks>
public readonly struct RainbowWaveEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new RainbowWaveEffect();
}
