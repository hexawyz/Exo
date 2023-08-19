using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer.LightingEffects;

/// <summary>Represents a light with a pulsing color effect alternating between two colors.</summary>
[TypeId(0x45E154D6, 0x1946, 0x4215, 0xA4, 0x4F, 0x97, 0x5C, 0xB7, 0x6D, 0xEE, 0xAE)]
public readonly struct TwoColorPulseEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color 1")]
	public RgbColor Color { get; }

	[DataMember(Order = 2)]
	[Display(Name = "Color 2")]
	public RgbColor SecondColor { get; }

	public TwoColorPulseEffect(RgbColor color, RgbColor secondColor)
	{
		Color = color;
		SecondColor = secondColor;
	}
}
