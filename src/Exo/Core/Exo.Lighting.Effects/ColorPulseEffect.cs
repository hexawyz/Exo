using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[TypeId(0xC2738C37, 0xE2E3, 0x4686, 0xB1, 0x6B, 0x30, 0x7A, 0x68, 0x3B, 0xA1, 0xA6)]
public readonly partial struct ColorPulseEffect(RgbColor color) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;
}
