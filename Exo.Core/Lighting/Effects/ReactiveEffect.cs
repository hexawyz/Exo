using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a reactive effect.</summary>
/// <remarks>Devices supporting this effect will usually display the chosen color as a reaction to an external event such as a click.</remarks>
[DataContract]
[TypeId(0xA175E0AD, 0xF649, 0x4F10, 0x99, 0xE2, 0xC3, 0xC9, 0x4D, 0x1C, 0x9B, 0xC7)]
public readonly struct ReactiveEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[DisplayName("Color")]
	public RgbColor Color { get; }

	public ReactiveEffect(RgbColor color) => Color = color;
}
