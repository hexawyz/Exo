namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors of the rainbow will move in a wave.</summary>
/// <remarks>
/// <para>
/// This is a slightly more advanced version of <see cref="ColorCycleEffect"/> that is designed for adressable lighting zones only.
/// The adressable lighting zones supporting this effect may not necessarily be adressable through software, but the effect will work as long as the lighting controller supports it.
/// </para>
/// <para>
/// Some lighting controllers may support applying this effect across multiple zones, as if they were a single addressable lighting zone.
/// </para>
/// </remarks>
[TypeId(0xB93254E0, 0xD39C, 0x40DF, 0xBF, 0x1F, 0x89, 0xD6, 0xCE, 0xB6, 0x16, 0x15)]
public readonly struct ColorWaveEffect : ISingletonLightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	public static ISingletonLightingEffect SharedInstance { get; } = new ColorWaveEffect();
}
