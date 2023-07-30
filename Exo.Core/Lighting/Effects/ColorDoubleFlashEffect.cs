using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a double-flashing color effect.</summary>
[EffectName("Color Double Flash")]
public readonly struct ColorDoubleFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorDoubleFlashEffect(RgbColor color) => Color = color;
}
