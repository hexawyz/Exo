using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[TypeId(0xE5403483, 0x217B, 0x4B82, 0x8F, 0x3D, 0x03, 0xB8, 0xC1, 0xE0, 0x48, 0xCA)]
public readonly partial struct Variable8ColorPulseEffect(in FixedArray8<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable8ColorPulseEffect>,
	IConvertibleLightingEffect<VariableColorPulseEffect, Variable8ColorPulseEffect>,
	IConvertibleLightingEffect<ColorPulse8Effect, Variable8ColorPulseEffect>
{
	public readonly FixedArray8<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable8ColorPulseEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorPulseEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable8ColorPulseEffect(in VariableColorPulseEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable8ColorPulseEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable8ColorPulseEffect(in ColorPulse8Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
