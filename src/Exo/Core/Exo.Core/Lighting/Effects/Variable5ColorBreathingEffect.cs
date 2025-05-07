using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a breathing color effect.</summary>
[TypeId(0xC324028F, 0x9157, 0x4BC2, 0x9B, 0x87, 0x0A, 0x01, 0x7D, 0x02, 0x50, 0xB2)]
public readonly partial struct Variable5ColorBreathingEffect(in FixedArray5<RgbColor> colors, PredeterminedEffectSpeed speed) :
	ILightingEffect,
	IConvertibleLightingEffect<ColorFlashEffect, Variable5ColorBreathingEffect>,
	IConvertibleLightingEffect<VariableColorBreathingEffect, Variable5ColorBreathingEffect>,
	IConvertibleLightingEffect<ColorBreathing5Effect, Variable5ColorBreathingEffect>
{
	public readonly FixedArray5<RgbColor> Colors = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public readonly PredeterminedEffectSpeed Speed = speed;

	public static implicit operator Variable5ColorBreathingEffect(in ColorFlashEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorBreathingEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = PredeterminedEffectSpeed.MediumFast;
		return dst;
	}

	public static implicit operator Variable5ColorBreathingEffect(in VariableColorBreathingEffect effect)
	{
		// Violates the immutability, but this should minimize the number of copies.
		Variable5ColorBreathingEffect dst;
		Unsafe.SkipInit(out dst);
		((Span<RgbColor>)Unsafe.AsRef(in dst.Colors)).Fill(effect.Color);
		Unsafe.AsRef(in dst.Speed) = effect.Speed;
		return dst;
	}

	public static implicit operator Variable5ColorBreathingEffect(in ColorBreathing5Effect effect)
		=> new(in effect.Colors, PredeterminedEffectSpeed.MediumFast);
}
