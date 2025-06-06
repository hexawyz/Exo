using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a pulsing color effect with 8-color zones.</summary>
[TypeId(0xD575547B, 0xF8B0, 0x4F80, 0x98, 0xDF, 0xE1, 0xA9, 0x11, 0x2C, 0x72, 0x70)]
public readonly partial struct ColorPulse8Effect(in FixedArray8<RgbColor> colors) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorPulseEffect, ColorPulse8Effect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	public static implicit operator ColorPulse8Effect(in ColorPulseEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorPulse8Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
