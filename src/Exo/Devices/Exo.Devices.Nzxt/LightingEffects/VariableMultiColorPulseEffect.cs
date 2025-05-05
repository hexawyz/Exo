using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>A pulse effect supporting multiple colors.</summary>
[TypeId(0xDDEDB156, 0xD897, 0x4AD5, 0x95, 0x66, 0x72, 0x1A, 0xF1, 0xAD, 0x46, 0xB0)]
public readonly partial struct VariableMultiColorPulseEffect(in FixedList8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorPulseEffect, VariableMultiColorPulseEffect>,
	IConvertibleLightingEffect<VariableColorPulseEffect, VariableMultiColorPulseEffect>
{
	[Display(Name = "Colors")]
	[Array(1, 8)]
	[DefaultValue("#51007A,")]
	public readonly FixedList8<RgbColor> Colors = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator VariableMultiColorPulseEffect(in ColorPulseEffect effect)
		=> new([effect.Color], PredeterminedEffectSpeed.MediumFast);

	public static implicit operator VariableMultiColorPulseEffect(in VariableColorPulseEffect effect)
		=> new([effect.Color], effect.Speed);
}
