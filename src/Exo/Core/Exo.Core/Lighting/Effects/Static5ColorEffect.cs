using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a 5-color zone with independent static colors.</summary>
[TypeId(0x2C64145E, 0x84E2, 0x42AB, 0xA5, 0xAC, 0xC6, 0x3E, 0x4B, 0x40, 0x3E, 0xDC)]
public readonly partial struct Static5ColorEffect(in FixedArray5<RgbColor> colors) :
	ILightingEffect,
	IConvertibleLightingEffect<StaticColorEffect, Static5ColorEffect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	public static implicit operator Static5ColorEffect(in StaticColorEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Static5ColorEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		return dst;
	}
}
