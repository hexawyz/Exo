using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Gigabyte.LightingEffects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[EffectName("Variable Color Pulse")]
public readonly struct VariableColorPulseEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 1)]
	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; }

	public VariableColorPulseEffect(RgbColor color, PredeterminedEffectSpeed speed) => Color = color;
}
