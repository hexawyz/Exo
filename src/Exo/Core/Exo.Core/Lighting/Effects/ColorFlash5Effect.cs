using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a flashing color effect with 5-color zones.</summary>
[TypeId(0x2FC31DBB, 0x1974, 0x4859, 0x90, 0xA3, 0xCE, 0x11, 0xC9, 0x24, 0x4B, 0xB1)]
public readonly partial struct ColorFlash5Effect :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, ColorFlash5Effect>
{
	public readonly FixedArray5<RgbColor> Colors;

	public ColorFlash5Effect(in FixedArray5<RgbColor> colors)
	{
		Colors = colors;
	}

	public static implicit operator ColorFlash5Effect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorFlash5Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
