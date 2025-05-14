using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>A color cycling effect supporting multiple colors.</summary>
[TypeId(0xA66FCE58, 0x4256, 0x4746, 0x8A, 0x86, 0x65, 0x33, 0xF6, 0xAE, 0x3E, 0xFB)]
public readonly partial struct VariableMultiColorCycleEffect(in FixedList8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<SpectrumCycleEffect, VariableMultiColorCycleEffect>,
	IConvertibleLightingEffect<VariableSpectrumCycleEffect, VariableMultiColorCycleEffect>
{
	[Display(Name = "Colors")]
	[Array(2, 8)]
	[DefaultValue("#51007A,#0000FF,#00FF00")]
	public readonly FixedList8<RgbColor> Colors = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	private static FixedList8<RgbColor> GetSpectrumColors()
	{
		FixedList8<RgbColor> colors = default;

		colors.Add(new(255, 0, 0));
		colors.Add(new(255, 127, 0));
		colors.Add(new(255, 255, 0));
		colors.Add(new(0, 255, 0));
		colors.Add(new(0, 0, 255));
		colors.Add(new(75, 0, 130));
		colors.Add(new(148, 0, 211));

		return colors;
	}

	public static implicit operator VariableMultiColorCycleEffect(in SpectrumCycleEffect effect)
		=> new(GetSpectrumColors(), PredeterminedEffectSpeed.MediumFast);

	public static implicit operator VariableMultiColorCycleEffect(in VariableSpectrumCycleEffect effect)
		=> new(GetSpectrumColors(), effect.Speed);
}
