using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Gigabyte.LightingEffects;

/// <summary>Represents a light with a flashing color effect.</summary>
[EffectName("Color Flash (Speed)")]
public readonly struct VariableColorFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public VariableColorFlashEffect(RgbColor color, PredeterminedEffectSpeed speed)
	{
		Color = color;
		Speed = speed;
	}
}
