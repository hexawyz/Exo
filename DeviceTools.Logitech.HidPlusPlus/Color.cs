using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
public readonly struct Color : IEquatable<Color>
{
	public byte R { get; init; }
	public byte G { get; init; }
	public byte B { get; init; }

	public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

	public override bool Equals(object? obj) => obj is Color color && Equals(color);
	public bool Equals(Color other) => R == other.R && G == other.G && B == other.B;
	public override int GetHashCode() => HashCode.Combine(R, G, B);

	public static bool operator ==(Color left, Color right) => left.Equals(right);
	public static bool operator !=(Color left, Color right) => !(left == right);
}
