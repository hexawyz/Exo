using System.ComponentModel.DataAnnotations;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where a color will move across an area following a wave pattern.</summary>
/// <remarks>This is the monochrome, less common, version of <see cref="SpectrumWaveEffect"/>.</remarks>
[TypeId(0xF64133DF, 0x043A, 0x4E9F, 0x82, 0xCA, 0x64, 0x89, 0xB8, 0xF7, 0x86, 0xA7)]
public readonly partial struct ColorWaveEffect(RgbColor color) : ISingleColorLightEffect
{
	[Display(Name = "Color")]
	public readonly RgbColor Color { get; } = color;
}
