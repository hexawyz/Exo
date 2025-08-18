using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.ColorFormats;

[DebuggerDisplay("R = {R}, G = {G}, B = {B}")]
[StructLayout(LayoutKind.Sequential, Size = 3)]
[JsonConverter(typeof(JsonConverter))]
public struct RgbColor : IColor, IEquatable<RgbColor>, IParsable<RgbColor>
{
	public sealed class JsonConverter : JsonConverter<RgbColor>
	{
		public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> Parse(reader.GetString()!, null);

		public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options)
		{
			Span<byte> buffer = stackalloc byte[7];
			buffer[0] = (byte)'#';
			value.R.TryFormat(buffer[1..], out _, "X2", CultureInfo.InvariantCulture);
			value.G.TryFormat(buffer[3..], out _, "X2", CultureInfo.InvariantCulture);
			value.B.TryFormat(buffer[5..], out _, "X2", CultureInfo.InvariantCulture);
			writer.WriteStringValue(buffer);
		}
	}

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

	/// <summary>Produces a linear interpolation between two colors, assuming linear RGB.</summary>
	/// <remarks>
	/// This method is not suitable for accurate color interpolations taking into account the sRGB curve or any gamma curve.
	/// It will still be good enough in many scenarios for a quick numeric interpolation of values.
	/// </remarks>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <param name="amount"></param>
	public static RgbColor Lerp(RgbColor a, RgbColor b, byte amount)
	{
		if (Vector64.IsHardwareAccelerated)
		{
			return Lerp(Vector64.Create((ushort)a.R, a.G, a.B, 0), Vector64.Create((ushort)b.R, b.G, b.B, 0), amount);
		}
		else if (Vector128.IsHardwareAccelerated)
		{
			return Lerp(ToVector128(a), ToVector128(b), amount);
		}
		else
		{
			return new
			(
				(byte)((a.R * ~amount + b.R * amount) / 255),
				(byte)((a.G * ~amount + b.G * amount) / 255),
				(byte)((a.B * ~amount + b.B * amount) / 255)
			);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector128<uint> ToVector128(RgbColor color)
	{
		if (Avx2.IsSupported)
		{
			return Ssse3.Shuffle(Vector128.CreateScalar<uint>(color.R | (uint)color.G << 8 | (uint)color.B << 16).AsByte(), Vector128.Create((byte)0, 4, 4, 4, 1, 4, 4, 4, 2, 4, 4, 4, 3, 4, 4, 4)).AsUInt32();
		}
		else
		{
			return Vector128.Create((uint)color.R, color.G, color.B, 0);
		}
	}

	private static RgbColor Lerp(Vector64<ushort> a, Vector64<ushort> b, byte amount)
	{
		var result = (a * Vector64.Create<ushort>((byte)~amount) + b * Vector64.Create<ushort>(amount)) / 255;
		return new((byte)result.GetElement(0), (byte)result.GetElement(1), (byte)result.GetElement(2));
	}

	private static RgbColor Lerp(Vector128<uint> a, Vector128<uint> b, byte amount)
	{
		var scaledUpResult = a * Vector128.Create<uint>((byte)~amount) + b * Vector128.Create<uint>(amount);
		if (Avx2.IsSupported)
		{
			var scaledDownResult = Vector256.ShiftRightLogical(Avx2.ConvertToVector256Int64(scaledUpResult).AsUInt64() * 0x80808081, 0x27);
			return new((byte)scaledDownResult.GetElement(0), (byte)scaledDownResult.GetElement(1), (byte)scaledDownResult.GetElement(2));
		}
		else
		{
			// TODO: Make this better by manually converting the 3 components rather than relying on vector fallback that will be slower than necessary.
			var scaledDownResult = scaledUpResult / (255 * 255);
			return new((byte)scaledDownResult.GetElement(0), (byte)scaledDownResult.GetElement(1), (byte)scaledDownResult.GetElement(2));
		}
	}
}
