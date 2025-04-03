using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors.LightingEffects;

/// <summary>Represents a static color effect using the second preset.</summary>
[TypeId(0x52F39EE8, 0xB4CD, 0x492B, 0xB9, 0x9B, 0xF4, 0x9A, 0xBB, 0x4C, 0xFB, 0x71)]
public readonly partial struct StaticColorPreset2Effect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new StaticColorPreset2Effect();
}
