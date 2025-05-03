using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>A blinking effect supporting multiple colors.</summary>
[TypeId(0xCB6F3794, 0x703F, 0x4ADA, 0x9E, 0x38, 0xE2, 0xD0, 0x50, 0x25, 0x2C, 0x08)]
public readonly partial struct VariableColorBlinkEffect(RgbColor color, PredeterminedEffectSpeed speed) : ILightingEffect
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
