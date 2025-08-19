using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where a color will move across an area following a wave pattern.</summary>
[TypeId(0x3798FD63, 0x6B69, 0x4167, 0xAD, 0x09, 0xB0, 0x06, 0x20, 0x82, 0x17, 0x8C)]
public readonly partial struct VariableColorWaveEffect(RgbColor color, PredeterminedEffectSpeed speed) :
	ISingleColorLightEffect,
	IConvertibleLightingEffect<ColorWaveEffect, VariableColorWaveEffect>
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	public static implicit operator VariableColorWaveEffect(in ColorWaveEffect effect)
		=> new(effect.Color, PredeterminedEffectSpeed.MediumFast);
}
