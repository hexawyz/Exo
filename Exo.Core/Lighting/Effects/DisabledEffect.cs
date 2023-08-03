namespace Exo.Lighting.Effects;

/// <summary>Represents a disabled light.</summary>
[TypeId(0x6B972C66, 0x0987, 0x4A0F, 0xA2, 0x0F, 0xCB, 0xFC, 0x1B, 0x0F, 0x3D, 0x4B)]
public readonly struct DisabledEffect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new DisabledEffect();
}
