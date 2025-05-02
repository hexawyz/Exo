using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a pulsing color effect with 5-color zones.</summary>
[TypeId(0xA0E2051C, 0xAB8B, 0x4D8C, 0xA9, 0x50, 0xFC, 0x01, 0xBA, 0x6C, 0x31, 0x2A)]
public readonly partial struct ColorPulse5Effect(in FixedArray5<RgbColor> colors) : ILightingEffect
{
	public readonly FixedArray5<RgbColor> Colors = colors;
}
