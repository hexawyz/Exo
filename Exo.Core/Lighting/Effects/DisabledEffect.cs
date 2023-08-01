namespace Exo.Lighting.Effects;

/// <summary>Represents a disabled light.</summary>
[EffectName("Disabled")]
public readonly struct DisabledEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new DisabledEffect();
}
