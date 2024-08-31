using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors.LightingEffects;

/// <summary>Represents a static color effect using the third preset.</summary>
[TypeId(0x511BA640, 0x295B, 0x425D, 0xB1, 0x8A, 0xEB, 0xED, 0x49, 0xC0, 0xA6, 0x48)]
public readonly struct StaticColorPreset3Effect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new StaticColorPreset3Effect();
}
