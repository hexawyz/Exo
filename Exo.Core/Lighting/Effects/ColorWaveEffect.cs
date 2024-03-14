using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

/// <summary>Represents an effect where a color will move across an area following a wave pattern.</summary>
/// <remarks>This is the monochrome, less common, version of <see cref="SpectrumWaveEffect"/>.</remarks>
[DataContract]
[TypeId(0xF64133DF, 0x043A, 0x4E9F, 0x82, 0xCA, 0x64, 0x89, 0xB8, 0xF7, 0x86, 0xA7)]
public readonly struct ColorWaveEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[DisplayName("Color")]
	public RgbColor Color { get; }

	public ColorWaveEffect(RgbColor color) => Color = color;
}
