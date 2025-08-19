using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a blinking color effect with 5-color zones.</summary>
[TypeId(0xE673CDFB, 0xC78F, 0x41DB, 0xA7, 0x17, 0x74, 0x80, 0x0A, 0xFD, 0x4E, 0x9E)]
public readonly partial struct ColorBlink5Effect :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBlinkEffect, ColorBlink5Effect>
{
	public readonly FixedArray5<RgbColor> Colors;

	public ColorBlink5Effect(in FixedArray5<RgbColor> colors)
	{
		Colors = colors;
	}

	public static implicit operator ColorBlink5Effect(in ColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorBlink5Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
