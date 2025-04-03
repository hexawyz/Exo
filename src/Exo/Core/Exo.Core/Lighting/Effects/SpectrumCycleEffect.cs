namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors will cycle through the color wheel.</summary>
[TypeId(0x2818D561, 0x15FB, 0x43B0, 0x9C, 0x2E, 0x9F, 0xF5, 0x08, 0x82, 0x2B, 0x7A)]
public readonly partial struct SpectrumCycleEffect : ISingletonLightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	public static ISingletonLightingEffect SharedInstance { get; } = new SpectrumCycleEffect();
}
