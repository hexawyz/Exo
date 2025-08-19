using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
[TypeId(0xCAF919F3, 0x19C1, 0x4706, 0xA4, 0x5F, 0x7F, 0x67, 0x6E, 0x2E, 0x29, 0xB2)]
public readonly partial struct Variable5ColorFlashEffect(in FixedArray5<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable5ColorFlashEffect>,
	IConvertibleLightingEffect<VariableColorFlashEffect, Variable5ColorFlashEffect>,
	IConvertibleLightingEffect<ColorFlash5Effect, Variable5ColorFlashEffect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable5ColorFlashEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorFlashEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable5ColorFlashEffect(in VariableColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorFlashEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable5ColorFlashEffect(in ColorFlash5Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
