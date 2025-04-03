using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
[TypeId(0xA3F80010, 0x5663, 0x4ECF, 0x9C, 0x22, 0x03, 0x6E, 0x28, 0x47, 0x8B, 0x7E)]
public readonly partial struct ColorFlashEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorFlashEffect(RgbColor color) => Color = color;
}
