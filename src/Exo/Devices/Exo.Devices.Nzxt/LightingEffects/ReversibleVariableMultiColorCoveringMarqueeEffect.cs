using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effects where the zone is gradually filled with one color after another, from start to end.</summary>
[TypeId(0x9460FD35, 0x6933, 0x42C5, 0xA8, 0xC6, 0x9A, 0x6B, 0x34, 0xEE, 0x3F, 0x3E)]
public readonly partial struct ReversibleVariableMultiColorCoveringMarqueeEffect(in FixedList8<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(1, 8)]
	[DefaultValue("#0000FF,#00FF00")]
	public readonly FixedList8<RgbColor> Colors = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public readonly PredeterminedEffectSpeed Speed = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public readonly EffectDirection1D Direction = direction;
}
