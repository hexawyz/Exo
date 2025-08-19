using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Nzxt.LightingEffects;

/// <summary>Represents a color effect that has semi-random dots appearing over the selected background color.</summary>
[TypeId(0x7A02F99B, 0xC515, 0x44DB, 0x95, 0xA0, 0xD7, 0xB8, 0xEA, 0xF3, 0x97, 0xCE)]
public readonly partial struct StarryNightEffect(RgbColor color, PredeterminedEffectSpeed speed) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	[DefaultValue("#51007A")]
	public RgbColor Color { get; } = color;
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(2)]
	public PredeterminedEffectSpeed Speed { get; } = speed;
}
