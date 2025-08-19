using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a flashing color effect with 8-color zones.</summary>
[TypeId(0x1D3B40F9, 0xAB2A, 0x43F4, 0x8A, 0x81, 0xC4, 0xBC, 0x90, 0x33, 0xB4, 0xA5)]
public readonly partial struct ColorFlash8Effect :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, ColorFlash8Effect>
{
	public readonly FixedArray8<RgbColor> Colors;

	public ColorFlash8Effect(in FixedArray8<RgbColor> colors)
	{
		Colors = colors;
	}

	public static implicit operator ColorFlash8Effect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorFlash8Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
