using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where a color will move across an area following a wave pattern.</summary>
/// <remarks>This is the monochrome, less common, version of <see cref="ReversibleVariableSpectrumWaveEffect"/>.</remarks>
[TypeId(0x2CB30144, 0x2586, 0x4780, 0x95, 0x6C, 0x43, 0x19, 0x8A, 0xF2, 0x72, 0x6F)]
public readonly partial struct ReversibleVariableColorWaveEffect(RgbColor color, PredeterminedEffectSpeed speed, EffectDirection1D direction) :
	ISingleColorLightEffect,
	IConvertibleLightingEffect<ColorWaveEffect, ReversibleVariableColorWaveEffect>,
	IConvertibleLightingEffect<VariableColorWaveEffect, ReversibleVariableColorWaveEffect>
{
	[Display(Name = "Color")]
	public RgbColor Color { get; } = color;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;

	public static implicit operator ReversibleVariableColorWaveEffect(in ColorWaveEffect effect)
		=> new(effect.Color, PredeterminedEffectSpeed.MediumFast, EffectDirection1D.Forward);

	public static implicit operator ReversibleVariableColorWaveEffect(in VariableColorWaveEffect effect)
		=> new(effect.Color, effect.Speed, EffectDirection1D.Forward);
}
