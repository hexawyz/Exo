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
		byte min;
		byte max;
		uint h;
		uint baseHue;
		byte componentA;
		byte componentB;
		if (rgb.R >= rgb.G)
		{
			if (rgb.R >= rgb.B)
			{
				max = rgb.R;
				componentA = rgb.G;
				componentB = rgb.B;
				if (rgb.G >= rgb.B)
				{
					min = rgb.B;
					if (max == min)
					{
						h = 0;
						goto ReturnColor;
					}
					baseHue = 0;
				}
				else
				{
					min = rgb.G;
					baseHue = 1530;
				}
				goto ComputeHue;
			}
			else
			{
				min = rgb.G;
				goto BaseHue1020;
			}
		}
		else if (rgb.G >= rgb.B)
		{
			max = rgb.G;
			baseHue = 510;
			componentA = rgb.B;
			componentB = rgb.R;
			min = rgb.B >= rgb.R ? rgb.R : rgb.B;
			goto ComputeHue;
		}
		else
		{
			min = rgb.R;
			goto BaseHue1020;
		}
	BaseHue1020:;
		max = rgb.B;
		baseHue = 1020;
		componentA = rgb.R;
		componentB = rgb.G;
		goto ComputeHue;
	ComputeHue:;
		h = ComputeHue(baseHue, componentA - componentB, (uint)(max - min));
	ReturnColor:;
		return new((ushort)h, ComputeSaturation(min, max), max);
	}

	private static uint ComputeHue(uint baseHue, int componentOffset, uint amplitude)
		=> componentOffset >= 0 ?
			baseHue + ReversibleDivision((uint)componentOffset, amplitude) :
			baseHue - ReversibleDivision((uint)-componentOffset, amplitude);

	private static byte ComputeSaturation(byte minimumComponent, byte brightness)
	{
		// We are looking to find the S value so that C = B * ~S / 255
		// 255 * C = B * ~S
		// ~S = 255 * C / B
		// S = ~(255 * C / B)
		if (brightness == 0) return 0;
		return (byte)ReversibleDivision2(minimumComponent, brightness);
	}

	// TODO: Make this suck less.
	// It is probably possible to do better than this. I do hope that it is possible.
	// Anyway, it will do for now.
	private static uint ReversibleDivision(uint a, uint b)
	{
		uint result = 255 * a / b;
		uint aa = b * result / 255;
		if (aa == a) return result;
		if (aa < a)
		{
			result++;
			if (b * result / 255 == a) return result;
			result++;
			if (b * result / 255 == a) return result;
		}
		else
		{
			result--;
			if (b * result / 255 == a) return result;
			result--;
			if (b * result / 255 == a) return result;
		}
		throw new InvalidOperationException();
	}

	private static uint ReversibleDivision2(uint a, uint b)
	{
		uint result = 255 * (b - a) / b;
		uint aa = b * (byte)~result / 255;
		if (aa == a) return result;
		if (aa < a)
		{
			result--;
			if (b * (byte)~result / 255 == a) return result;
			result--;
			if (b * (byte)~result / 255 == a) return result;
		}
		else
		{
			result++;
			if (b * (byte)~result / 255 == a) return result;
			result++;
			if (b * (byte)~result / 255 == a) return result;
		}
		throw new InvalidOperationException();
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
