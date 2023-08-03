using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Gigabyte.LightingEffects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[EffectName("Color Pulse (Advanced)")]
public readonly struct AdvancedColorPulseEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	[DataMember(Order = 2)]
	[Display(Name = "Fade In")]
	[Range(0, 65535)]
	[DefaultValue(100)]
	public ushort FadeIn { get; }

	[DataMember(Order = 3)]
	[Display(Name = "Fade Out")]
	[Range(0, 65535)]
	[DefaultValue(100)]
	public ushort FadeOut { get; }

	[DataMember(Order = 4)]
	[Display(Name = "Duration")]
	[Range(0, 65535)]
	[DefaultValue(1800)]
	public ushort Duration { get; }

	public AdvancedColorPulseEffect(RgbColor color, ushort fadeIn, ushort fadeOut, ushort duration)
	{
		Color = color;
		FadeIn = fadeIn;
		FadeOut = fadeOut;
		Duration = duration;
	}
}
