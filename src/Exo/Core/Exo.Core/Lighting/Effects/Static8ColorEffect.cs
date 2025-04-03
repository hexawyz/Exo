using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a 8-color zone with independent static colors.</summary>
[DataContract]
[TypeId(0x476F95EB, 0x1D55, 0x46D6, 0xA0, 0x97, 0x9F, 0x9C, 0xC3, 0x4B, 0x56, 0xE5)]
public readonly partial struct Static8ColorEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	public readonly FixedArray8<RgbColor> Colors;

	public Static8ColorEffect(in FixedArray8<RgbColor> colors)
	{
		Colors = colors;
	}
}
