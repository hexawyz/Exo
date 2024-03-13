using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a chasing color effect, for a zone that has more than one light.</summary>
[TypeId(0x9FD48D3C, 0x9BD5, 0x403E, 0x8F, 0xFD, 0x8F, 0xF6, 0x6F, 0x47, 0xB2, 0x32)]
public readonly struct ColorChaseEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Color")]
	public RgbColor Color { get; }

	public ColorChaseEffect(RgbColor color) => Color = color;
}
