using System;
using System.Runtime.InteropServices;

namespace Exo
{
	[StructLayout(LayoutKind.Sequential, Size = 24)]
	public struct RgbColor : IEquatable<RgbColor>
	{
		public byte R;
		public byte G;
		public byte B;

		public RgbColor(byte r, byte g, byte b)
			=> (R, G, B) = (r, g, b);

		public override bool Equals(object? obj) => obj is RgbColor color && Equals(color);
		public bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
		public override int GetHashCode() => HashCode.Combine(R, G, B);

		public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);
		public static bool operator !=(RgbColor left, RgbColor right) => !(left == right);
	}
}
