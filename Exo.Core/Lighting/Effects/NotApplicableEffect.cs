namespace Exo.Lighting.Effects;

/// <summary>Represents .</summary>
[TypeId(0xC771A454, 0xCAE5, 0x41CF, 0x91, 0x21, 0xBE, 0xF8, 0xAD, 0xC3, 0x80, 0xED)]
public readonly struct NotApplicableEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new NotApplicableEffect();
}
