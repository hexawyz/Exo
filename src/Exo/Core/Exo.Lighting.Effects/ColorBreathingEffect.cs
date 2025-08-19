using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a breathing color effect.</summary>
/// <remarks>
/// Breathing and pulse effects are very similar.
/// Breathing would generally be the effect where ascent and descent are symmetrical, while pulse would be the one where ascent is quick and descent slower.
/// </remarks>
[TypeId(0x83F205B0, 0xD73F, 0x4A22, 0xBD, 0xDF, 0xDC, 0xE1, 0xFD, 0xE2, 0x07, 0x10)]
public readonly partial struct ColorBreathingEffect(RgbColor color) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;
}
