using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.ColorFormats;

[StructLayout(LayoutKind.Sequential, Size = 3)]
public struct RgbColor : IEquatable<RgbColor>
{
	public byte R;
	public byte G;
	public byte B;

	public RgbColor(byte r, byte g, byte b)
		=> (R, G, B) = (r, g, b);

	public static RgbColor FromInt32(int value) => new((byte)(value >>> 16), (byte)(value >>> 8), (byte)value);

	public int ToInt32() => R << 16 | G << 8 | B;
	public uint ToUInt32(byte alpha) => (uint)(alpha << 24 | R << 16 | G << 8 | B);

	public override bool Equals(object? obj) => obj is RgbColor color && Equals(color);
	public bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
	public override int GetHashCode() => HashCode.Combine(R, G, B);

	public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);
	public static bool operator !=(RgbColor left, RgbColor right) => !(left == right);
}
