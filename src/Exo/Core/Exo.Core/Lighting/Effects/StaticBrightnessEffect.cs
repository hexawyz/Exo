using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a monochrome light controlled only by brightness .</summary>
[DataContract]
[TypeId(0xED8D205B, 0xE693, 0x48C0, 0x8E, 0xC3, 0x72, 0x03, 0xF7, 0x67, 0x20, 0x2F)]
public readonly partial struct StaticBrightnessEffect : IBrightnessLightingEffect
{
	[DataMember(Order = 1)]
	[Display(Name = "Brightness")]
	public byte BrightnessLevel { get; }

	public StaticBrightnessEffect(byte brightnessLevel) => BrightnessLevel = brightnessLevel;
}
