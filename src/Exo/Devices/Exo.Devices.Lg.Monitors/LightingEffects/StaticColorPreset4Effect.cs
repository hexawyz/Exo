using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors.LightingEffects;

/// <summary>Represents a static color effect using the fourth preset.</summary>
[TypeId(0x2959B1E6, 0xC0D4, 0x4D5B, 0xA8, 0x0B, 0xC0, 0x6D, 0x7E, 0x2C, 0x4B, 0x74)]
public readonly partial struct StaticColorPreset4Effect : ILightingEffect
{
	/// <summary>Returns a boxed instance of the effect.</summary>
	public static readonly ILightingEffect SharedInstance = new StaticColorPreset4Effect();
}
