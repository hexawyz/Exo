using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a blinking color effect.</summary>
[TypeId(0x3B52A6DE, 0x5A06, 0x49F0, 0xA1, 0xED, 0x4E, 0x2D, 0x24, 0x2E, 0x1E, 0x8C)]
public readonly partial struct Variable8ColorBlinkEffect(in FixedArray8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBlinkEffect, Variable8ColorBlinkEffect>,
	IConvertibleLightingEffect<VariableColorBlinkEffect, Variable8ColorBlinkEffect>,
	IConvertibleLightingEffect<ColorBlink8Effect, Variable8ColorBlinkEffect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable8ColorBlinkEffect(in ColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorBlinkEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable8ColorBlinkEffect(in VariableColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorBlinkEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable8ColorBlinkEffect(in ColorBlink8Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
