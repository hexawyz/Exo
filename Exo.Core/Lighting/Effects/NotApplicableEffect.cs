namespace Exo.Lighting.Effects;

/// <summary>Represents .</summary>
public readonly struct NotApplicableEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new NotApplicableEffect();
}
