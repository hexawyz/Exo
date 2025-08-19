using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Exo.ColorFormats;

namespace Exo.Lighting.Effects;

/// <summary>Represents an effect where a sequence of colors will move across the lighting zone.</summary>
[TypeId(0xFA123622, 0x8B93, 0x4862, 0xA1, 0x24, 0x96, 0xB6, 0xAD, 0xF0, 0x84, 0x89)]
public readonly partial struct ReversibleVariableMultiColorWaveEffect(in FixedList10<RgbColor> colors, PredeterminedEffectSpeed speed, EffectDirection1D direction, byte size, bool interpolate) :
	ILightingEffect,
	IProgrammableLightingEffect<RgbColor>
{
	[Display(Name = "Colors")]
	[Array(2, 10)]
	[DefaultValue("#0000FF,#00FFFF,#000000")]
	public FixedList10<RgbColor> Colors { get; } = colors;

	[Display(Name = "Speed")]
	[Range(0, 5)]
	[DefaultValue(3)]
	public PredeterminedEffectSpeed Speed { get; } = speed;

	[Display(Name = "Direction")]
	[DefaultValue(EffectDirection1D.Forward)]
	public EffectDirection1D Direction { get; } = direction;

	[Display(Name = "Size")]
	[DefaultValue(10)]
	[Range(1, 20)]
	public byte Size { get; } = size;

	[Display(Name = "Interpolate")]
	[DefaultValue(true)]
	public bool Interpolate { get; } = interpolate;

	ImmutableArray<LightingEffectFrame<RgbColor>> IProgrammableLightingEffect<RgbColor>.GetEffectFrames(int ledCount, int capacity)
		=> ImmutableCollectionsMarshal.AsImmutableArray
		(
			Interpolate ?
				AddressableEffectHelper.GenerateInterpolatedSlidingSceneFrames(Colors, Speed, Direction, Size, ledCount) :
				AddressableEffectHelper.GenerateSlidingSceneFrames(Colors, Speed, Direction, Size, ledCount)
		);

	static bool IAddressableLightingEffect.CanUseLargerFramesForSmallerSizes => true;
}
