using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a breathing color effect with 8-color zones.</summary>
[TypeId(0x0A17146F, 0x6EBD, 0x440D, 0xAF, 0x8C, 0xF5, 0x4F, 0x59, 0x0D, 0x72, 0xDC)]
public readonly partial struct ColorBreathing8Effect(in FixedArray8<RgbColor> colors) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBreathingEffect, ColorBreathing8Effect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	public static implicit operator ColorBreathing8Effect(in ColorBreathingEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorBreathing8Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
