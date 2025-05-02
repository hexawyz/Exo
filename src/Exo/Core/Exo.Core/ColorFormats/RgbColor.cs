using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Exo.ColorFormats;

[DebuggerDisplay("R = {R}, G = {G}, B = {B}")]
[StructLayout(LayoutKind.Sequential, Size = 3)]
public struct RgbColor : IEquatable<RgbColor>, IParsable<RgbColor>
{
	public byte R;
	public byte G;
	public byte B;

	public RgbColor(byte r, byte g, byte b)
		=> (R, G, B) = (r, g, b);

	public static RgbColor FromInt32(int value) => new((byte)(value >>> 16), (byte)(value >>> 8), (byte)value);

	public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");

	public readonly int ToInt32() => R << 16 | G << 8 | B;
	public readonly uint ToUInt32(byte alpha) => (uint)(alpha << 24 | R << 16 | G << 8 | B);

	public readonly override bool Equals(object? obj) => obj is RgbColor color && Equals(color);
	public readonly bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
	public readonly override int GetHashCode() => HashCode.Combine(R, G, B);

	public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);
	public static bool operator !=(RgbColor left, RgbColor right) => !(left == right);

	public static RgbColor Parse(string? s, IFormatProvider? provider)
	{
		ArgumentNullException.ThrowIfNull(s);
		if (s.Length == 7 && s[0] == '#' && int.TryParse(s.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int color))
		{
			return new((byte)(color >> 16), (byte)(color >> 8), (byte)color);
		}
		else
		{
			throw new ArgumentException();
		}
	}

	public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out RgbColor result)
	{
		if (s is not null && s.Length == 7 && s[0] == '#' && int.TryParse(s.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int color))
		{
			result = new((byte)(color >> 16), (byte)(color >> 8), (byte)color);
			return true;
		}
		result = default;
		return false;
	}
}
