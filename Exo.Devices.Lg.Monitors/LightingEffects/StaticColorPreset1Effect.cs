using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors.LightingEffects;

/// <summary>Represents a static color effect using the first preset.</summary>
[TypeId(0x85397AFD, 0xFFC7, 0x4C0C, 0x91, 0x45, 0x6D, 0xF9, 0x1D, 0x55, 0x68, 0x66)]
public readonly struct StaticColorPreset1Effect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new StaticColorPreset1Effect();
}
