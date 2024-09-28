using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a blinking color effect.</summary>
/// <remarks>This is similar to the flash effect, except that the on and off states have the same period.</remarks>
[TypeId(0x46BE7D40, 0x8A97, 0x486C, 0x97, 0xCD, 0x07, 0xE9, 0x31, 0xF4, 0x6A, 0xB9)]
public readonly struct ColorBlinkEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorBlinkEffect(RgbColor color) => Color = color;
}
