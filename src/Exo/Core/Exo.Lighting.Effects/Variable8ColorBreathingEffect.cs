using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a breathing color effect.</summary>
[TypeId(0xCCE8BEDC, 0x9C6F, 0x46C8, 0xAC, 0x2E, 0xD2, 0xED, 0x45, 0x4B, 0x14, 0xE9)]
public readonly partial struct Variable8ColorBreathingEffect(in FixedArray8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable8ColorBreathingEffect>,
	IConvertibleLightingEffect<VariableColorBreathingEffect, Variable8ColorBreathingEffect>,
	IConvertibleLightingEffect<ColorBreathing8Effect, Variable8ColorBreathingEffect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable8ColorBreathingEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorBreathingEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable8ColorBreathingEffect(in VariableColorBreathingEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorBreathingEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable8ColorBreathingEffect(in ColorBreathing8Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
