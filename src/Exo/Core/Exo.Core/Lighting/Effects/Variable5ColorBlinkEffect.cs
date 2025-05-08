using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a blinking color effect.</summary>
[TypeId(0x774C7CFC, 0x86BF, 0x43DD, 0xAF, 0x88, 0x4F, 0xF1, 0xE4, 0x80, 0xDA, 0x55)]
public readonly partial struct Variable5ColorBlinkEffect(in FixedArray5<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorBlinkEffect, Variable5ColorBlinkEffect>,
	IConvertibleLightingEffect<VariableColorBlinkEffect, Variable5ColorBlinkEffect>,
	IConvertibleLightingEffect<ColorBlink5Effect, Variable5ColorBlinkEffect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable5ColorBlinkEffect(in ColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorBlinkEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable5ColorBlinkEffect(in VariableColorBlinkEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorBlinkEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable5ColorBlinkEffect(in ColorBlink5Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
