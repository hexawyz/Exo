using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Exo.ColorFormats;

[DebuggerDisplay("R = {R}, G = {G}, B = {B}")]
[StructLayout(LayoutKind.Sequential, Size = 3)]
public struct RgbColor : IEquatable<RgbColor>
{
	public byte R;
	public byte G;
	public byte B;

	public RgbColor(byte r, byte g, byte b)
		=> (R, G, B) = (r, g, b);

	public static RgbColor FromInt32(int value) => new((byte)(value >>> 16), (byte)(value >>> 8), (byte)value);

	public readonly int ToInt32() => R << 16 | G << 8 | B;
	public readonly uint ToUInt32(byte alpha) => (uint)(alpha << 24 | R << 16 | G << 8 | B);

	public readonly override bool Equals(object? obj) => obj is RgbColor color && Equals(color);
	public readonly bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
	public readonly override int GetHashCode() => HashCode.Combine(R, G, B);

	public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);
	public static bool operator !=(RgbColor left, RgbColor right) => !(left == right);
}
