using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Exo.Lighting.Effects;

[TypeId(0xF6A8C369, 0xD230, 0x4E63, 0xB6, 0x00, 0xA4, 0x4F, 0x1B, 0x3B, 0xBE, 0xCA)]
public readonly partial struct ReversibleVariableSpectrumWaveEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) :
	ILightingEffect,
	IConvertibleLightingEffect<SpectrumWaveEffect, ReversibleVariableSpectrumWaveEffect>,
	IConvertibleLightingEffect<VariableSpectrumWaveEffect, ReversibleVariableSpectrumWaveEffect>
{
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;

	public static implicit operator ReversibleVariableSpectrumWaveEffect(in SpectrumWaveEffect effect)
		=> new(PredeterminedEffectSpeed.MediumFast, EffectDirection1D.Forward);

	public static implicit operator ReversibleVariableSpectrumWaveEffect(in VariableSpectrumWaveEffect effect)
		=> new(effect.Speed, EffectDirection1D.Forward);
}
