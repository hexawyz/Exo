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
public struct HsvColor : IEquatable<HsvColor>
{
	public ushort H;
	public byte S;
	public byte V;

	public HsvColor(ushort h, byte s, byte b)
	{
		// NB: The very important logic here is that in the color circle, the values 0 and 255 are merged together,
		// so there are actually only 255 values per slice, and not 256. (One component is always 0 when another is 255)
		// So, in total, we have 6 * 255 = 1530 values, starting at 0 and ending at 1529.
		ArgumentOutOfRangeException.ThrowIfGreaterThan(h, 1529);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(s, 255);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(b, 255);
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
		var shiftedHue = Vector128.Create((int)H) + Vector128.Create([0, 4 * 255, 2 * 255, 0]);
		var hueColor = Vector128.Clamp(Vector128.Abs(shiftedHue - Vector128.BitwiseAnd(Vector128.GreaterThanOrEqual(shiftedHue, twoPi), twoPi) - Vector128.Create(3 * 255)) - Vector128.Create(255), Vector128<int>.Zero, maxComponent);
		var scaledSaturatedHueColor = (Vector128.Create((int)V) * (hueColor * Vector128.Create((int)S) + maxComponent * Vector128.Create((int)(byte)~S))).AsUInt32();
		if (Avx2.IsSupported)
		{
			var scaledDownComponents = Vector256.ShiftRightLogical(Avx2.ConvertToVector256Int64(scaledSaturatedHueColor).AsUInt64() * 0x81018203, 0x2f);
			return new((byte)scaledDownComponents.GetElement(0), (byte)scaledDownComponents.GetElement(1), (byte)scaledDownComponents.GetElement(2));
		}
		else
		{
			var scaledDownComponents = scaledSaturatedHueColor / (255 * 255);
			return new((byte)scaledDownComponents.GetElement(0), (byte)scaledDownComponents.GetElement(1), (byte)scaledDownComponents.GetElement(2));
		}
	}

	public static HsvColor FromRgb(RgbColor rgb)
	{
		uint min;
		uint max;
		uint h;
		if (rgb.R >= rgb.G)
		{
			if (rgb.R >= rgb.B)
			{
				max = rgb.R;
				if (rgb.G >= rgb.B)
				{
					min = rgb.B;
					h = max == min ? 0 : 255 * ((uint)rgb.G - rgb.B) / (max - min);
				}
				else
				{
					min = rgb.G;
					h = 1530 - 255 * ((uint)rgb.B - rgb.G) / (max - min);
				}
			}
			else
			{
				max = rgb.B;
				min = rgb.G;
				h = 1020 + 255 * ((uint)rgb.R - rgb.G) / (max - min);
			}
		}
		else if (rgb.G >= rgb.B)
		{
			max = rgb.G;
			if (rgb.B >= rgb.R)
			{
				min = rgb.R;
				h = 510 + 255 * ((uint)rgb.B - rgb.R) / (max - min);
			}
			else
			{
				min = rgb.B;
				h = 510 - 255 * ((uint)rgb.R - rgb.B) / (max - min);
			}
		}
		else
		{
			max = rgb.B;
			min = rgb.R;
			h = 1020 - 255 * ((uint)rgb.G - rgb.R) / (max - min);
		}

		return new((ushort)h, max == 0 ? (byte)0 : (byte)(255 * (max - min) / max), (byte)max);
	}

	public static ushort GetScaledHue(float hue)
	{
		if (hue >= 1) hue = hue % 1;
		return (ushort)(hue * 1530);
	}

	public static float GetStandardHue(ushort scaledHue)
	{
		if (scaledHue >= 1530) scaledHue = (ushort)(scaledHue % 1530);
		return 360U * scaledHue / 1530f;
	}

	public static byte GetScaledSaturation(float saturation)
	{
		if (saturation > 100) saturation = saturation % 100;
		return (byte)(saturation * 255);
	}

	public static float GetStandardSaturation(byte scaledSaturation)
	{
		return 100U * scaledSaturation / 255f;
	}

	public static byte GetScaledValue(float value) => GetScaledSaturation(value);

	public static float GetStandardValue(byte scaledValue) => GetStandardSaturation(scaledValue);
}
