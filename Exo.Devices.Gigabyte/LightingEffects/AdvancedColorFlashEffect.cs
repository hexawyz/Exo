using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Devices.Gigabyte.LightingEffects;

/// <summary>Represents a light with a flashing color effect.</summary>
[TypeId(0x99D1CFC4, 0xB25D, 0x4EEF, 0xAB, 0x17, 0x05, 0x2D, 0xF3, 0xD4, 0x93, 0x2D)]
public readonly struct AdvancedColorFlashEffect : ISingleColorLightEffect
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

	[DataMember(Order = 5)]
	[Display(Name = "Flash Count")]
	[Range(1, 255)]
	[DefaultValue(1)]
	public byte FlashCount { get; }

	public AdvancedColorFlashEffect(RgbColor color, ushort fadeIn, ushort fadeOut, ushort duration, byte flashCount)
	{
		Color = color;
		FadeIn = fadeIn;
		FadeOut = fadeOut;
		Duration = duration;
		FlashCount = flashCount;
	}
}
