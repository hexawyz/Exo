using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

[TypeId(0xF6A8C369, 0xD230, 0x4E63, 0xB6, 0x00, 0xA4, 0x4F, 0x1B, 0x3B, 0xBE, 0xCA)]
public readonly partial struct ReversibleVariableSpectrumWaveEffect(PredeterminedEffectSpeed speed, EffectDirection1D direction) :
	ILightingEffect,
	IConvertibleLightingEffect<SpectrumWaveEffect, ReversibleVariableSpectrumWaveEffect>,
	IConvertibleLightingEffect<VariableSpectrumWaveEffect, ReversibleVariableSpectrumWaveEffect>,
	IProgrammableLightingEffect<RgbColor>
{
	// These are the same colors used by the Elgato software. (ScenesCommon.RAINBOW_COLORS)
	// Honestly, they are not that good. Keeping them here in case they can be useful.
	private static ReadOnlySpan<byte> ElgatoRainbowColorBytes =>
	[
		255, 0, 0, // red
		255, 165, 0, // orange
		255, 255, 0, // yellow
		0, 128, 0, // green
		0, 0, 255, // blue
		75, 0, 130, // indigo
		238, 130, 238  // violet
	];

	// These rainbow colors are the one from RGB Fusion. They do look better, so we'll use them for now.
	private static ReadOnlySpan<byte> GigabyteRainbowColorBytes =>
	[
		255, 0, 0,
		255, 127, 0,
		255, 255, 0,
		0, 255, 0,
		0, 0, 255,
		75, 0, 130,
		148, 0, 211,
	];

	// More standard rainbow colors. (Ignoring any gamma curve. No idea how LEDs are calibrated anyway)
	private static ReadOnlySpan<byte> RainbowColorBytes =>
	[
		255, 0, 0,
		255, 128, 0,
		255, 255, 0,
		0, 255, 0,
		0, 0, 255,
		128, 0, 255,
		255, 0, 255,
	];

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;

	public static implicit operator ReversibleVariableSpectrumWaveEffect(in SpectrumWaveEffect effect)
		=> new(PredeterminedEffectSpeed.MediumFast, EffectDirection1D.Forward);

	public static implicit operator ReversibleVariableSpectrumWaveEffect(in VariableSpectrumWaveEffect effect)
		=> new(effect.Speed, EffectDirection1D.Forward);

	ImmutableArray<LightingEffectFrame<RgbColor>> IProgrammableLightingEffect<RgbColor>.GetEffectFrames(int ledCount, int capacity)
		=> ImmutableCollectionsMarshal.AsImmutableArray(AddressableEffectHelper.GenerateInterpolatedSlidingSceneFrames(MemoryMarshal.Cast<byte, RgbColor>(RainbowColorBytes), Speed, Direction, 5, ledCount));

	static bool IAddressableLightingEffect.CanUseLargerFramesForSmallerSizes => true;
}
