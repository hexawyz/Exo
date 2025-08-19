using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents color effects where the addressable zone is always evenly split between the two colors, with colors zones moving from start to end.</summary>
[TypeId(0xD8C6DCE9, 0x2902, 0x452F, 0x91, 0x21, 0xD9, 0x6B, 0x7E, 0xED, 0x1B, 0xA0)]
public readonly partial struct TaiChiEffect(FixedArray2<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Colors")]
	[DefaultValue("#FF0000,#0000FF")]
	public FixedArray2<RgbColor> Colors { get; } = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
}
