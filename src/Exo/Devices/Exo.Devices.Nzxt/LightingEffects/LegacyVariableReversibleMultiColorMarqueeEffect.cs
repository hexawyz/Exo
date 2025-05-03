using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect where a block of color is moving from start to end.</summary>
[TypeId(0x82E3BA40, 0x7557, 0x4D51, 0xAE, 0xDD, 0x9C, 0xBF, 0xA2, 0x7E, 0x10, 0x7B)]
public readonly partial struct LegacyVariableReversibleMultiColorMarqueeEffect(ImmutableArray<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction, byte size) : ILightingEffect
{
	[Display(Name = "Colors")]
	[Array(1, 8)]
	[DefaultValue("#0000FF,#00FF00")]
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
