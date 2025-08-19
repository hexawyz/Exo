using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
[TypeId(0x2DA41D23, 0x511A, 0x4071, 0xAA, 0xB5, 0x65, 0x54, 0x8F, 0x45, 0xC2, 0x81)]
public readonly partial struct Variable8ColorFlashEffect(in FixedArray8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable8ColorFlashEffect>,
	IConvertibleLightingEffect<VariableColorFlashEffect, Variable8ColorFlashEffect>,
	IConvertibleLightingEffect<ColorFlash8Effect, Variable8ColorFlashEffect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable8ColorFlashEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorFlashEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable8ColorFlashEffect(in VariableColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorFlashEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable8ColorFlashEffect(in ColorFlash8Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
