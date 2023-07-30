using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[EffectName("Color Pulse")]
public readonly struct ColorPulseEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorPulseEffect(RgbColor color) => Color = color;
}
