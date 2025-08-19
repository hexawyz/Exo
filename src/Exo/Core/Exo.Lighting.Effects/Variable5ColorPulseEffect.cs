using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
[TypeId(0x34EC164F, 0x33A6, 0x4F50, 0x8C, 0xB3, 0xE2, 0x39, 0xDB, 0x41, 0xB8, 0xDB)]
public readonly partial struct Variable5ColorPulseEffect(in FixedArray5<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable5ColorPulseEffect>,
	IConvertibleLightingEffect<VariableColorPulseEffect, Variable5ColorPulseEffect>,
	IConvertibleLightingEffect<ColorPulse5Effect, Variable5ColorPulseEffect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable5ColorPulseEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorPulseEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable5ColorPulseEffect(in VariableColorPulseEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorPulseEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable5ColorPulseEffect(in ColorPulse5Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
