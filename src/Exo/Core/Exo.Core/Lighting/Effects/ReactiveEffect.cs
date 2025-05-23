using System.ComponentModel;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a reactive effect.</summary>
/// <remarks>Devices supporting this effect will usually display the chosen color as a reaction to an external event such as a click.</remarks>
[TypeId(0xA175E0AD, 0xF649, 0x4F10, 0x99, 0xE2, 0xC3, 0xC9, 0x4D, 0x1C, 0x9B, 0xC7)]
public readonly partial struct ReactiveEffect(RgbColor color) : ISingleColorLightEffect
{
	[DisplayName("Color")]
	public RgbColor Color { get; } = color;
}
