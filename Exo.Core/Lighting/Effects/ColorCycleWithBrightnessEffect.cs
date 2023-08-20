using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where colors will cycle through the color wheel with adjustable brightness.</summary>
[TypeId(0x7CDBCE50, 0x63FA, 0x42A4, 0x92, 0xE1, 0xDE, 0x7D, 0x91, 0x52, 0x1F, 0x4D)]
public readonly struct ColorCycleWithBrightnessEffect : IBrightnessLightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Brightness")]
	public byte BrightnessLevel { get; }

	public ColorCycleWithBrightnessEffect(byte brightnessLevel) => BrightnessLevel = brightnessLevel;
}
