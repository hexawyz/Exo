using System.ComponentModel;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a 5-color zone with independent static colors.</summary>
[DataContract]
[TypeId(0x2C64145E, 0x84E2, 0x42AB, 0xA5, 0xAC, 0xC6, 0x3E, 0x4B, 0x40, 0x3E, 0xDC)]
public readonly struct Static5ColorEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	[DisplayName("Color 1")]
	public RgbColor Color { get; }

	[DataMember(Order = 2)]
	[DisplayName("Color 2")]
	public RgbColor Color1 { get; }

	[DataMember(Order = 3)]
	[DisplayName("Color 3")]
	public RgbColor Color2 { get; }

	[DataMember(Order = 4)]
	[DisplayName("Color 4")]
	public RgbColor Color3 { get; }

	[DataMember(Order = 5)]
	[DisplayName("Color 5")]
	public RgbColor Color4 { get; }

	public Static5ColorEffect(RgbColor color, RgbColor color1, RgbColor color2, RgbColor color3, RgbColor color4)
	{
		Color = color;
		Color1 = color1;
		Color2 = color2;
		Color3 = color3;
		Color4 = color4;
	}
}
