using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Gigabyte.LightingEffects;

/// <summary>Represents a light with a double-flashing color effect.</summary>
[EffectName("Color Double Flash (Speed)")]
public readonly struct VariableColorDoubleFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public VariableColorDoubleFlashEffect(RgbColor color, PredeterminedEffectSpeed speed)
	{
		Color = color;
		Speed = speed;
	}
}
