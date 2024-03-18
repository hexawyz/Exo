using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a flashing color effect with 8-color zones.</summary>
[DataContract]
[TypeId(0x1D3B40F9, 0xAB2A, 0x43F4, 0x8A, 0x81, 0xC4, 0xBC, 0x90, 0x33, 0xB4, 0xA5)]
public readonly struct ColorFlash8Effect : ILightingEffect
{
	[DataMember(Order = 1)]
	public readonly FixedArray8<RgbColor> Colors;

	public ColorFlash8Effect(in FixedArray8<RgbColor> colors)
	{
		Colors = colors;
	}
}
