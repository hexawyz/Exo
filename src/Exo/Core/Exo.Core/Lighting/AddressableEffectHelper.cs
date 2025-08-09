using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Lighting;

// Helper methods used to build basic addressable effects.
public static class AddressableEffectHelper
{
	// TODO: Implement easing functions so that we can have fancier things happening, both for color and delays.

	private static ReadOnlySpan<ushort> StandardFrameDelays => [300, 200, 100, 50, 25, 10];

	public static LightingEffectFrame<RgbColor>[] GenerateSlidingSceneFrames(ReadOnlySpan<RgbColor> colorSequence, PredeterminedEffectSpeed speed, EffectDirection1D direction, int colorSize, int ledCount)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(ledCount, 1);

		int frameCount = colorSize * colorSequence.Length;
		ushort delay = StandardFrameDelays[(byte)speed];
		var frames = new LightingEffectFrame<RgbColor>[frameCount];
		int startingColorIndex = 0;
		int startingDuplicateIndex = 0;
		for (int i = 0; i < frames.Length; i++)
		{
			var colors = GC.AllocateUninitializedArray<RgbColor>(ledCount);
			int colorIndex = startingColorIndex;
			int duplicateIndex = startingDuplicateIndex;
			for (int j = 0; j < colors.Length; j++)
			{
				colors[j] = colorSequence[colorIndex];
				if (++duplicateIndex >= colorSize)
				{
					duplicateIndex = 0;
					if (++colorIndex >= colorSequence.Length) colorIndex = 0;
				}

			}
			frames[i] = new(colors, delay);
			if (direction == EffectDirection1D.Forward)
			{
				if ((uint)--startingDuplicateIndex >= colorSize)
				{
					startingDuplicateIndex = colorSize - 1;
					if ((uint)--startingColorIndex >= colorSequence.Length) startingColorIndex = colorSequence.Length - 1;
				}
			}
			else
			{
				if (++startingDuplicateIndex >= colorSize)
				{
					startingDuplicateIndex = 0;
					if (++startingColorIndex >= colorSequence.Length) startingColorIndex = 0;
				}
			}
		}
		return frames;
	}

	public static LightingEffectFrame<RgbColor>[] GenerateInterpolatedSlidingSceneFrames(ReadOnlySpan<RgbColor> colorSequence, PredeterminedEffectSpeed speed, EffectDirection1D direction, int colorSize, int ledCount)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(colorSize, 1);

		if (colorSequence.Length < 2) return GenerateSlidingSceneFrames(colorSequence, speed, direction, ledCount, colorSize);
		if (colorSize < 2) return GenerateSlidingSceneFrames(colorSequence, speed, direction, ledCount);

		var colors = new RgbColor[colorSequence.Length * colorSize];
		int k = 0;
		for (int i = 0; i < colorSequence.Length; i++)
		{
			var a = colorSequence[i];
			int j = i + 1;
			if (j >= colorSequence.Length) j = 0;
			var b = colorSequence[j];
			for (j = 0; j < colorSize; j++)
			{
				colors[k++] = RgbColor.Lerp(a, b, (byte)(255 * j / (uint)colorSize));
			}
		}
		return GenerateSlidingSceneFrames(colors, speed, direction, ledCount);
	}

	public static LightingEffectFrame<RgbColor>[] GenerateSlidingSceneFrames(ReadOnlySpan<RgbColor> colorSequence, PredeterminedEffectSpeed speed, EffectDirection1D direction, int ledCount)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(ledCount, 1);

		int frameCount = colorSequence.Length;
		ushort delay = StandardFrameDelays[(byte)speed];
		var frames = new LightingEffectFrame<RgbColor>[frameCount];
		var colors = GC.AllocateUninitializedArray<RgbColor>(ledCount);
		int rowOffset = colorSequence.Length > 1 ? colorSequence.Length - 1 : 0;
		// Initialize the color buffer that will be rotated.
		// When the led count is greater than the number of colors in the base sequence, this will be more efficient.
		// Otherwise, it might be more efficient to always fetch from the original buffer, which is an improvement that can be done later.
		for (int i = 0; ;)
		{
			int count = colors.Length - i;
			if (colorSequence.Length > count)
			{
				colorSequence[..count].CopyTo(colors.AsSpan(i));
				if (direction != EffectDirection1D.Forward) rowOffset = count;
				break;
			}
			else
			{
				colorSequence.CopyTo(colors.AsSpan(i));
				if ((i += colorSequence.Length) >= colors.Length) break;
			}
		}
		frames[0] = new(colors, delay);
		var lastColors = colors;
		for (int i = 1; i < frames.Length; i++)
		{
			colors = new RgbColor[ledCount];
			if (direction == EffectDirection1D.Forward)
			{
				lastColors.AsSpan(0, lastColors.Length - 1).CopyTo(colors.AsSpan(1));
				colors[0] = colorSequence[rowOffset];
				if ((uint)--rowOffset >= colorSequence.Length) rowOffset = colorSequence.Length - 1;
			}
			else
			{
				lastColors.AsSpan(1).CopyTo(colors);
				colors[^1] = colorSequence[rowOffset];
				if (++rowOffset >= colorSequence.Length) rowOffset = 0;
			}
			frames[i] = new(colors, delay);
			lastColors = colors;
		}
		return frames;
	}
}
