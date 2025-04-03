using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a double-flashing color effect.</summary>
[TypeId(0x2C497719, 0x8477, 0x4FE2, 0x80, 0x45, 0x48, 0x89, 0x39, 0xD4, 0xC9, 0x13)]
public readonly partial struct ColorDoubleFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorDoubleFlashEffect(RgbColor color) => Color = color;
}
