using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a breathing color effect.</summary>
[TypeId(0xB6C72241, 0xED04, 0x4431, 0x81, 0xB6, 0xAE, 0xA2, 0xA6, 0x02, 0x90, 0x92)]
public readonly partial struct VariableColorBreathingEffect(RgbColor color, PredeterminedEffectSpeed speed) :
	ISingleColorLightEffect,
	IConvertibleLightingEffect<ColorBreathingEffect, VariableColorBreathingEffect>
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	public static implicit operator VariableColorBreathingEffect(in ColorBreathingEffect effect)
		=> new(effect.Color, PredeterminedEffectSpeed.MediumFast);
}
