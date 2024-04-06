using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.ColorFormats;

[DebuggerDisplay("A = {A}, R = {R}, G = {G}, B = {B}")]
[StructLayout(LayoutKind.Sequential, Size = 3)]
public struct ArgbColor : IEquatable<ArgbColor>
{
	public byte A;
	public byte R;
	public byte G;
	public byte B;

	public ArgbColor(RgbColor color, byte a)
		=> (A, R, G, B) = (a, color.R, color.G, color.B);

	public ArgbColor(byte r, byte g, byte b, byte a)
		=> (A, R, G, B) = (a, r, g, b);

	public static ArgbColor FromInt32(int value) => new((byte)(value >>> 24), (byte)(value >>> 16), (byte)(value >>> 8), (byte)value);

	public readonly int ToInt32() => R << 16 | G << 8 | B;
	public readonly uint ToUInt32(byte alpha) => (uint)(alpha << 24 | R << 16 | G << 8 | B);

	public readonly override bool Equals(object? obj) => obj is ArgbColor color && Equals(color);
	public readonly bool Equals(ArgbColor other) => Unsafe.As<byte, uint>(ref Unsafe.AsRef(in A)) == Unsafe.As<byte, uint>(ref other.A);
	public readonly override int GetHashCode() => HashCode.Combine(R, G, B);

	public static bool operator ==(ArgbColor left, ArgbColor right) => left.Equals(right);
	public static bool operator !=(ArgbColor left, ArgbColor right) => !(left == right);
}
