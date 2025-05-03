using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents color effects where the addressable zone is always evenly split between the two colors, with colors zones moving from start to end.</summary>
[TypeId(0x9460FD35, 0x6933, 0x42C5, 0xA8, 0xC6, 0x9A, 0x6B, 0x34, 0xEE, 0x3F, 0x3E)]
public readonly partial struct CoveringMarqueeEffect(ImmutableArray<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction, byte size) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(1, 8)]
	//[DefaultValue("#0000FF,#00FF00")]
	public ImmutableArray<RgbColor> Colors { get; } = colors;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;
	[Display(Name = "Size")]
	[Range(3, 6)]
	[DefaultValue(3)]
	public byte Size { get; } = size;
}
