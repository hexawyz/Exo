using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace DeviceTools;

public readonly struct PnpVendorId :
#if NET6_0_OR_GREATER
	ISpanFormattable,
#if NET7_0_OR_GREATER
	IParsable<PnpVendorId>,
	ISpanParsable<PnpVendorId>,
#if NET8_0_OR_GREATER
	IUtf8SpanFormattable,
	IUtf8SpanParsable<PnpVendorId>,
#endif
#endif
#endif
	IEquatable<PnpVendorId>
{
	/// <summary>This is the raw value of the manufacturer ID.</summary>
	public readonly ushort Value { get; }

	public static PnpVendorId FromRaw(ushort value)
	{
		if (!IsValueValid(value)) throw new ArgumentOutOfRangeException(nameof(value));
		return new PnpVendorId(value);
	}

	public static PnpVendorId Parse(string text)
	{
		if (text is null) throw new ArgumentNullException(text);

		return Parse(text.AsSpan());
	}

	public static PnpVendorId Parse(ReadOnlySpan<char> text)
	{
		if (!TryParse(text, out var vendorId))
		{
			throw new ArgumentException("Valid PNP IDs must be composed of three letters.");
		}

		return vendorId;
	}

	public static PnpVendorId Parse(ReadOnlySpan<byte> text)
	{
		if (!TryParse(text, out var vendorId))
		{
			throw new ArgumentException("Valid PNP IDs must be composed of three letters.");
		}

		return vendorId;
	}

	public static bool TryParse(string? text, out PnpVendorId vendorId)
	{
		if (text is null) throw new ArgumentNullException(text);

		return TryParse(text.AsSpan(), out vendorId);
	}

	public static bool TryParse(ReadOnlySpan<char> text, out PnpVendorId vendorId)
	{
		if (text.Length != 3 || !IsLetter(text[0]) || !IsLetter(text[1]) || !IsLetter(text[2]))
		{
			vendorId = default;
			return false;
		}

		vendorId = new PnpVendorId((ushort)(short)((text[0] & ~0x20 - 'A' + 1) << 10 | (text[1] & ~0x20 - 'A' + 1) << 5 | (text[2] & ~0x20 - 'A' + 1)));
		return true;
	}

	public static bool TryParse(ReadOnlySpan<byte> text, out PnpVendorId vendorId)
	{
		if (text.Length != 3 || !IsLetter(text[0]) || !IsLetter(text[1]) || !IsLetter(text[2]))
		{
			vendorId = default;
			return false;
		}

		vendorId = new PnpVendorId((ushort)(short)((text[0] & ~0x20 - 'A' + 1) << 10 | (text[1] & ~0x20 - 'A' + 1) << 5 | (text[2] & ~0x20 - 'A' + 1)));
		return true;
	}

	public static PnpVendorId Parse(string text, IFormatProvider? provider)
		=> Parse(text);

#if NETSTANDARD2_0
	public static bool TryParse(string? text, IFormatProvider? provider, out PnpVendorId vendorId)
#else
	public static bool TryParse([NotNullWhen(true)] string? text, IFormatProvider? provider, [MaybeNullWhen(false)] out PnpVendorId vendorId)
#endif
		=> TryParse(text, out vendorId);

	public static PnpVendorId Parse(ReadOnlySpan<char> text, IFormatProvider? provider)
		=> Parse(text);

#if NETSTANDARD2_0
	public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out PnpVendorId vendorId)
#else
	public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, [MaybeNullWhen(false)] out PnpVendorId vendorId)
#endif
		=> TryParse(text, out vendorId);

	public static PnpVendorId Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
		=> Parse(utf8Text);

#if NETSTANDARD2_0
	public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out PnpVendorId vendorId)
#else
	public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, [MaybeNullWhen(false)] out PnpVendorId vendorId)
#endif
		=> TryParse(utf8Text, out vendorId);

	private static bool IsLetter(char c)
		=> c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';

	private static bool IsLetter(byte b)
		=> b >= 'A' && b <= 'Z' || b >= 'a' && b <= 'z';

	private static bool IsValueValid(ushort value)
		=> value <= 0b11010_11010_11010 && (value & 0b11111_11111) <= 0b11010_11010 && (value & 0b11111) <= 11010;

	private PnpVendorId(ushort value) => Value = value;

	public bool IsValid => IsValueValid(Value);

	public override bool Equals(object? obj) => obj is PnpVendorId id && Equals(id);
	public bool Equals(PnpVendorId other) => Value == other.Value;

#if NETSTANDARD2_0
	public override int GetHashCode() => Value.GetHashCode();
#else
	public override int GetHashCode() => HashCode.Combine(Value);
#endif

#if NETSTANDARD2_0
	public override string ToString()
	{
		if (IsValid)
		{
			Span<char> s = stackalloc char[3];
			ushort v = Value;

			(s[0], s[1], s[2]) = ((char)('A' - 1 + (v >> 10)), (char)('A' - 1 + ((v >> 5) & 0x1f)), (char)('A' - 1 + (v & 0x1f)));
			return s.ToString();
		}
		else
		{
			return Value.ToString("X4");
		}
	}
#else
	public override string ToString()
		=> IsValid ?
			string.Create(3, Value, (s, v) => (s[0], s[1], s[2]) = ((char)('A' - 1 + (v >> 10)), (char)('A' - 1 + ((v >> 5) & 0x1f)), (char)('A' - 1 + (v & 0x1f)))) :
			Value.ToString("X4");
#endif

	public bool TryFormat(Span<char> destination, out int charsWritten)
	{
		if (IsValid)
		{
			if (destination.Length >= 3)
			{
				(destination[0], destination[1], destination[2]) = ((char)('A' - 1 + (Value >> 10)), (char)('A' - 1 + ((Value >> 5) & 0x1f)), (char)('A' - 1 + (Value & 0x1f)));
				charsWritten = 3;
				return true;
			}
			else
			{
				charsWritten = 0;
				return false;
			}
		}
		else
		{
#if NETSTANDARD2_0
			if (destination.Length >= 4)
			{
				Value.ToString("X4").AsSpan().CopyTo(destination);
				charsWritten = 4;
				return true;
			}
			else
			{
				charsWritten = 0;
				return false;
			}
#else
			return Value.TryFormat(destination, out charsWritten, "X4", CultureInfo.InvariantCulture);
#endif
		}
	}

	public bool TryFormat(Span<byte> destination, out int bytesWritten)
	{
		if (IsValid)
		{
			if (destination.Length >= 3)
			{
				(destination[0], destination[1], destination[2]) = ((byte)('A' - 1 + (Value >> 10)), (byte)('A' - 1 + ((Value >> 5) & 0x1f)), (byte)('A' - 1 + (Value & 0x1f)));
				bytesWritten = 3;
				return true;
			}
			else
			{
				bytesWritten = 0;
				return false;
			}
		}
		else
		{
#if NETSTANDARD2_0
			if (destination.Length >= 4)
			{
				Encoding.ASCII.GetBytes(Value.ToString("X4")).AsSpan().CopyTo(destination);
				bytesWritten = 4;
				return true;
			}
			else
			{
				bytesWritten = 0;
				return false;
			}
#elif !NET8_0_OR_GREATER
			return Utf8Formatter.TryFormat(Value, destination, out bytesWritten, new('X', 4));
#else
			return Value.TryFormat(destination, out bytesWritten, "X4", CultureInfo.InvariantCulture);
#endif
		}
	}

	public string ToString(string? format, IFormatProvider? formatProvider)
		=> ToString();

	public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		=> TryFormat(destination, out charsWritten);

	public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		=> TryFormat(utf8Destination, out bytesWritten);

	public static bool operator ==(PnpVendorId left, PnpVendorId right) => left.Equals(right);
	public static bool operator !=(PnpVendorId left, PnpVendorId right) => !(left == right);
}
