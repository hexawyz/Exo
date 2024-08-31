using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a pulsing color effect with 8-color zones.</summary>
[DataContract]
[TypeId(0xD575547B, 0xF8B0, 0x4F80, 0x98, 0xDF, 0xE1, 0xA9, 0x11, 0x2C, 0x72, 0x70)]
public readonly struct ColorPulse8Effect : ILightingEffect
{
	[DataMember(Order = 1)]
	public readonly FixedArray8<RgbColor> Colors;

	public ColorPulse8Effect(in FixedArray8<RgbColor> colors)
	{
		Colors = colors;
	}
}
