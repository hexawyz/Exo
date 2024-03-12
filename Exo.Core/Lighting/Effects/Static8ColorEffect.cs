using System.ComponentModel;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a 8-color zone with independent static colors.</summary>
[DataContract]
[TypeId(0x476F95EB, 0x1D55, 0x46D6, 0xA0, 0x97, 0x9F, 0x9C, 0xC3, 0x4B, 0x56, 0xE5)]
public readonly struct Static8ColorEffect : ILightingEffect
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

	[DataMember(Order = 6)]
	[DisplayName("Color 6")]
	public RgbColor Color5 { get; }

	[DataMember(Order = 7)]
	[DisplayName("Color 7")]
	public RgbColor Color6 { get; }

	[DataMember(Order = 8)]
	[DisplayName("Color 8")]
	public RgbColor Color7 { get; }

	public Static8ColorEffect(RgbColor color, RgbColor color1, RgbColor color2, RgbColor color3, RgbColor color4, RgbColor color5, RgbColor color6, RgbColor color7)
	{
		Color = color;
		Color1 = color1;
		Color2 = color2;
		Color3 = color3;
		Color4 = color4;
		Color5 = color5;
		Color6 = color6;
		Color7 = color7;
	}
}
