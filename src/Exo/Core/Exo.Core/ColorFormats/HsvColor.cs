using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Exo.ColorFormats;

/// <summary>Represents a HSV color in a "perfect" format allowing roundtrip from and to 24-bit RGB.</summary>
/// <remarks>
/// <para>
/// In order to avoid precision loss, the exponents are expanded to larger maximum values.
/// The goal of this representation is to allow an exact mapping from one R8G8B8 value to one canonical HSB value.
/// Of course, due to the way HSV works, multiple HSV values can map to the same RGB color.
/// The opposite, however, shouldn't be the case, and <c>ToRgb(FromRgb(color))</c> should always return the original color.
/// </para>
/// <para>
/// Saturation and Brightness use values from 0 to 255, with 255 representing 100%.
/// Hue is represented by values between 0 and 1529, with 1530 representing 360°.
/// </para>
/// <para>
/// Conversion to the standard HSB representation is relatively straightforward for those who need it, of course.
/// Hue can just be scaled by <c>60 / 255</c>, Saturation and Brightness can be scaled by <c>100 / 255</c>.
/// </para>
/// </remarks>
[DebuggerDisplay("H = {H}, S = {S}, V = {V}")]
[StructLayout(LayoutKind.Sequential, Size = 4)]
public readonly struct HsvColor : IColor, IEquatable<HsvColor>
{
	public readonly ushort H;
	public readonly byte S;
	public readonly byte V;

	public HsvColor(ushort h, byte s, byte b)
	{
		// NB: The very important logic here is that in the color circle, the values 0 and 255 are merged together,
		// so there are actually only 255 values per slice, and not 256. (One component is always 0 when another is 255)
		// So, in total, we have 6 * 255 = 1530 values, starting at 0 and ending at 1529.
		ArgumentOutOfRangeException.ThrowIfGreaterThan(h, 1529);
		(H, S, V) = (h, s, b);
	}

	public readonly override bool Equals(object? obj) => obj is HsvColor color && Equals(color);
	public readonly bool Equals(HsvColor other) => H == other.H && S == other.S && V == other.V;
	public readonly override int GetHashCode() => HashCode.Combine(H, S, V);

	public static bool operator ==(HsvColor left, HsvColor right) => left.Equals(right);
	public static bool operator !=(HsvColor left, HsvColor right) => !(left == right);

	public RgbColor ToRgb()
	{
		var twoPi = Vector128.Create(6 * 255);
		var maxComponent = Vector128.Create(255);
		var shiftedHue = Vector128.Create((int)H) + Vector128.Create(0, 4 * 255, 2 * 255, 0);
		var hueColor = Vector128.Clamp(Vector128.Abs(shiftedHue - Vector128.BitwiseAnd(Vector128.GreaterThanOrEqual(shiftedHue, twoPi), twoPi) - Vector128.Create(3 * 255)) - Vector128.Create(255), Vector128<int>.Zero, maxComponent);
		var scaledSaturatedHueColor = (hueColor * Vector128.Create(S * V) + Vector128.Create(255 * V * (byte)~S)).AsUInt32();
		if (Avx2.IsSupported)
		{
			var scaledDownComponents = Vector256.ShiftRightLogical(Avx2.ConvertToVector256Int64(scaledSaturatedHueColor).AsUInt64() * 0x81018203, 0x2f);
			return new((byte)scaledDownComponents.GetElement(0), (byte)scaledDownComponents.GetElement(1), (byte)scaledDownComponents.GetElement(2));
		}
		else
		{
			// TODO: Optimize
			var scaledDownComponents = scaledSaturatedHueColor / (255 * 255);
			return new((byte)scaledDownComponents.GetElement(0), (byte)scaledDownComponents.GetElement(1), (byte)scaledDownComponents.GetElement(2));
		}
	}

	public static HsvColor FromRgb(RgbColor rgb)
	{
		// Max is always directly brightness, other values are scaled relative to brightness and need to be rescaled to 255 in a way that round-trips.
		// Min is inverse saturation. (i.e. if 0, S = 255 and if B, S = 0)
		// Med is the hue offset. Positive or negative, relative to which components are Max and Med.
		byte min;
		byte med;
		byte max;
		bool isPositiveOffset = false;
		uint baseHue;
		if (rgb.R >= rgb.G)
		{
			if (rgb.R >= rgb.B)
			{
				max = rgb.R;
				if (isPositiveOffset = rgb.G >= rgb.B)
				{
					min = rgb.B;
					// Obvious special case when R = G = B, we have a grayscale.
					if (max == min) return new(0, 0, max);
					med = rgb.G;
					baseHue = 0;
				}
				else
				{
					min = rgb.G;
					med = rgb.B;
					baseHue = 1530;
				}
				goto ComputeHue;
			}
			else
			{
				min = rgb.G;
				med = rgb.R;
				isPositiveOffset = true;
				goto BaseHue1020;
			}
		}
		else if (rgb.G >= rgb.B)
		{
			max = rgb.G;
			baseHue = 510;
			if (isPositiveOffset = rgb.B >= rgb.R)
			{
				min = rgb.R;
				med = rgb.B;
			}
			else
			{
				min = rgb.B;
				med = rgb.R;
			}
			goto ComputeHue;
		}
		else
		{
			min = rgb.R;
			med = rgb.G;
			goto BaseHue1020;
		}
	BaseHue1020:;
		max = rgb.B;
		baseHue = 1020;
	ComputeHue:;
		// Special cases when the hue is "pure", we need only a single division.
		if (min == med)
		{
			return new((ushort)baseHue, (byte)~ReversibleDivision(min, max), max);
		}
		else if (med == max)
		{
			return new((ushort)(isPositiveOffset ? baseHue + 255 : baseHue - 255), (byte)~ReversibleDivision(min, max), max);
		}
		// For now, to deal with the annoying integer division stuff, we'll deconstruct the RGB color one HSV component at a time.
		// First, we rescale all three components before computing the rest. (This means that for all intents and purposes max is now 255)
		// It does require 3 extra divisions, which is all but great, but at least it will make the computations perfect.
		// (At the very best one of those divisions should simply go away, because one component is the max)
		//med = (byte)ReversibleDivision(med, max);
		// Then, undo the effect from the saturation.

		// X = 255 * (Y - Z) / (V - Z)
		(med, min) = ReverseSaturationAndHue(min, med, max);

		return new((ushort)(isPositiveOffset ? baseHue + med : baseHue - med), (byte)~min, max);
	}

	private static (byte HueOffset, byte InverseSaturation) ReverseSaturationAndHue(byte min, byte med, byte max)
	{
		// Each integeger division that occurs in the HSL to RGB process has an error 0 ≤ e ≤ 1.
		// Depending on which component, the error can be restricted by another component, but we don't have a way to compute the error itself.
		// Therefore, we use annoying logic to verify and enforce that we find *one* of the values that properly compute the RGB color we want.
		byte inverseSaturation = ReversibleDivision(min, max);
		// X = 255 * (Y - Z) / (V - Z) <=> X = 255 * ((Y + eY) - Z) / (V - (Z + eZ))
		uint result = 255 * (uint)(med - min) / (uint)(max - min);
		// This is the formula to compute the final component value based on min and max,
		// but it does not allow finding the hue value that we want.
		// We need to use the saturation computed independently so that we can gaurantee everything to be computed as expected.
		//uint c = (result * max + (255 - result) * min) / 255;
		// Y = (X * V * S + 255 * V * (255 - S)) / (255 * 255)
		if (max * (result * (byte)~inverseSaturation + 255U * inverseSaturation) / (255 * 255) != med) result++;
		return ((byte)result, inverseSaturation);
	}

	// This is empirically verified to work for all values.
	// Hopefully, there would be a less anoying way to compute this,
	// but this version does not include too many extra operations, so it will do.
	private static byte ReversibleDivision(byte a, byte b)
	{
		if (b == 255) return a;
		uint result = 255U * a / b;
		if (b * result / 255U  != a) ++result;
		return (byte)result;
	}

	public static ushort GetScaledHue(float hue)
	{
		if (hue >= 360) hue = hue % 360;
		return (ushort)(hue * (1530f / 360));
	}

	public static float GetStandardHueSingle(ushort scaledHue)
	{
		if (scaledHue >= 1530) scaledHue = (ushort)(scaledHue % 1530);
		return 360U * scaledHue / 1530f;
	}

	public static ushort GetStandardHueUInt16(ushort scaledHue)
	{
		if (scaledHue >= 1530) scaledHue = (ushort)(scaledHue % 1530);
		return (ushort)(360U * scaledHue / 1530f);
	}

	public static byte GetScaledSaturation(byte saturation)
	{
		if (saturation < 0) return 0;
		if (saturation > 100) return 255;
		return (byte)(saturation * 255U / 100);
	}

	public static byte GetScaledSaturation(float saturation)
	{
		if (saturation < 0) return 0;
		if (saturation > 100) return 255;
		return (byte)(saturation * 255 / 100);
	}

	public static float GetStandardSaturationSingle(byte scaledSaturation)
		=> 100U * scaledSaturation / 255f;

	public static byte GetStandardSaturationByte(byte scaledSaturation)
		=> (byte)((100U * scaledSaturation + 128) / 255);

	public static byte GetScaledValue(byte value) => GetScaledSaturation(value);
	public static byte GetScaledValue(float value) => GetScaledSaturation(value);

	public static float GetStandardValueSingle(byte scaledValue) => GetStandardSaturationSingle(scaledValue);
	public static byte GetStandardValueByte(byte scaledValue) => GetStandardSaturationByte(scaledValue);
}
