using System.ComponentModel;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a static color.</summary>
[TypeId(0x2A30CB46, 0x8BF2, 0x4F0E, 0x98, 0x35, 0x77, 0x4E, 0xB0, 0x2D, 0x24, 0x8D)]
public readonly partial struct StaticColorEffect(RgbColor color) : ISingleColorLightEffect
{
	[DisplayName("Color")]
	public RgbColor Color { get; } = color;
}
