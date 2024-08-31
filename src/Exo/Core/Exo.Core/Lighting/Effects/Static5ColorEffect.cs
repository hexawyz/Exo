using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a 5-color zone with independent static colors.</summary>
[DataContract]
[TypeId(0x2C64145E, 0x84E2, 0x42AB, 0xA5, 0xAC, 0xC6, 0x3E, 0x4B, 0x40, 0x3E, 0xDC)]
public readonly struct Static5ColorEffect : ILightingEffect
{
	[DataMember(Order = 1)]
	public readonly FixedArray5<RgbColor> Colors;

	public Static5ColorEffect(in FixedArray5<RgbColor> colors)
	{
		Colors = colors;
	}
}
