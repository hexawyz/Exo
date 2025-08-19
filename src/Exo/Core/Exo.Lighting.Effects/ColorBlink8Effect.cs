using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a blinking color effect with 8-color zones.</summary>
[TypeId(0xC3408D21, 0xA029, 0x4A2E, 0x84, 0xBA, 0xB8, 0xD6, 0x1D, 0x55, 0x21, 0x06)]
public readonly partial struct ColorBlink8Effect :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBlinkEffect, ColorBlink8Effect>
{
	public readonly FixedArray8<RgbColor> Colors;

	public ColorBlink8Effect(in FixedArray8<RgbColor> colors)
	{
		Colors = colors;
	}

	public static implicit operator ColorBlink8Effect(in ColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorBlink8Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
