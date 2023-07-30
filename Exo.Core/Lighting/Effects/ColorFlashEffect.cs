using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
[EffectName("Color Flash")]
public readonly struct ColorFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorFlashEffect(RgbColor color) => Color = color;
}
