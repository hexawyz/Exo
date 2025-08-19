using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a breathing color effect with 5-color zones.</summary>
[TypeId(0x18C638B6, 0xA168, 0x411C, 0x89, 0xE2, 0x26, 0x7C, 0x74, 0xE8, 0x23, 0x09)]
public readonly partial struct ColorBreathing5Effect(in FixedArray5<RgbColor> colors) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBreathingEffect, ColorBreathing5Effect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	public static implicit operator ColorBreathing5Effect(in ColorBreathingEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		ColorBreathing5Effect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
