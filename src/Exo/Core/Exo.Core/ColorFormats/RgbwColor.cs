using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Exo.ColorFormats;

[DebuggerDisplay("R = {R}, G = {G}, B = {B}, W = {W}")]
[StructLayout(LayoutKind.Sequential, Size = 4)]
public struct RgbwColor : IEquatable<RgbwColor>
{
	public byte W;
	public byte R;
	public byte G;
	public byte B;

	public static RgbwColor FromRgb(RgbColor rgb)
	{
		// This implements a very approximative RGB to RGBW conversion, but it's better than nothing.
		byte w = Math.Min(Math.Min(rgb.R, rgb.G), rgb.B);
		return new RgbwColor { W = w, R = rgb.R, G = rgb.G, B = rgb.B };
	}

	public RgbwColor(byte r, byte g, byte b, byte w)
		=> (W, R, G, B) = (w, r, g, b);

	public static RgbwColor FromInt32(int value) => new((byte)(value >>> 16), (byte)(value >>> 8), (byte)value, (byte)(value >>> 24));

	public RgbColor ToRgb()
	{
		// This implements a very approximative RGBW to conversion, but it's better than nothing.
		// We'll have a conflict when the sum of Max(R, G, B) + W is greater than 255, so the conversion is not reversible.
		ushort r = (ushort)(W + R);
		ushort g = (ushort)(W + G);
		ushort b = (ushort)(W + B);

		ushort max = Math.Max(Math.Max(r, g), b);

		return max <= 255 ? new RgbColor((byte)r, (byte)g, (byte)b) : new RgbColor((byte)(r * 255 / max), (byte)(r * 255 / max), (byte)(r * 255 / max));
	}

	public int ToInt32() => W << 24 | R << 16 | G << 8 | B;

	public override bool Equals(object? obj) => obj is RgbColor color && Equals(color);
	public bool Equals(RgbwColor other) => W == other.W && R == other.R && G == other.G && B == other.B;
	public override int GetHashCode() => HashCode.Combine(W, R, G, B);

	public static bool operator ==(RgbwColor left, RgbwColor right) => left.Equals(right);
	public static bool operator !=(RgbwColor left, RgbwColor right) => !(left == right);
}
