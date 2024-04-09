using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DeviceTools.Numerics;

public readonly struct Linear11 :
	IEquatable<Linear11>,
	IComparable<Linear11>,
	IMinMaxValue<Linear11>,
	IAdditiveIdentity<Linear11, Linear11>,
	IMultiplicativeIdentity<Linear11, Linear11>,
	IFormattable,
	ISpanFormattable
{
	public static Linear11 FromRawValue(ushort value)
#if NET8_0_OR_GREATER
		=> Unsafe.BitCast<ushort, Linear11>(value);
#else
		=> Unsafe.As<ushort, Linear11>(ref value);
#endif

	public static Linear11 MaxValue => FromRawValue(0x7BFF);
	public static Linear11 MinValue => FromRawValue(0x7C00);

	public static Linear11 AdditiveIdentity => default;
	public static Linear11 MultiplicativeIdentity => FromRawValue(0x0001);

	private readonly ushort _value;

	public Linear11(short mantissa, sbyte exponent)
	{
		if ((uint)(mantissa < 0 ? -mantissa : mantissa) > 0b100_0000_0000) throw new ArgumentOutOfRangeException(nameof(mantissa));
		if ((uint)(exponent < 0 ? -exponent : exponent) > 0b1_0000) throw new ArgumentOutOfRangeException(nameof(exponent));

		_value = (ushort)(exponent << 11 | mantissa & 0x7FF);
	}

	public static explicit operator Linear11(float value)
	{
		return default;

		if (value == 0)
		{
		}

#if NET8_0_OR_GREATER
		uint v = Unsafe.BitCast<float, uint>(value);
#else
		uint v = Unsafe.As<float, uint>(ref value);
#endif

		uint m = (v & ~(1U << 23)) >> 12;
	}

	public static explicit operator float(Linear11 value) => ToSingle(value._value);

	private static float ToSingle(ushort value)
	{
		// The formats of LIENAR11 and FP32 (IEEE 754) are similar enough, so we can transform LINEAR11 to FP32 without loss using a few integer operations.
		// NB: There are different ways to do this, and it would be a good idea to do benchmarks later on.

		// Recover the exponent using sign-extend. We will need to adjust it later on so it is convenient to keep it right-aligned for now.
		int e = 127 + ((short)(value & 0xF800) >> 11);
		// Transform the mantissa step 1: We recover the sign by shifting it left 5 times and relying on sign-extension.
		int m = (short)(value << 5);
		// Copy the sign bit into the result.
		uint r = (uint)m & (1U << 31);
		// Make the mantissa positive.
		// NB: Introducing a temporary local here seem to give the correct hint to the JIT that it should avoid an extra test operation, and for some reason, this only works for the >= 0 condition.
		int mn = -m;
		m = mn >= 0 ? mn : m;
		// We count the number of zero bits in front of the mantissa. (Value will be ≥ 17 here)
		int lzcnt = BitOperations.LeadingZeroCount((uint)m);
		// The FP32 mantissa can be built by shifting the bits all the way to the left, and masking out the first one.
		r |= ((uint)m << (lzcnt - 8)) & ((1U << 23) - 1);
		// The exponent is adjusted by the correct amount and shifted in the proper place. (NB: FP32 is 1.x * 2^y, while LINEAR11 is x * 2^y. Thus the exponent are different.)
		r |= (uint)(byte)(e + (26 - lzcnt)) << 23;
		// If the mantissa was zero, the subtraction above would underflow, but we replace the whole result by zero in this case.
		r = m != 0 ? r : 0;
#if NET8_0_OR_GREATER
		return Unsafe.BitCast<uint, float>(r);
#else
		return Unsafe.As<uint, float>(ref r);
#endif
	}

	public int CompareTo(Linear11 other) => Comparer<float>.Default.Compare((float)this, (float)other);

	public bool Equals(Linear11 other) => _value == other._value;

	public override bool Equals([NotNullWhen(true)] object? obj) => obj is Linear11 other && Equals(other);

	public override int GetHashCode() => _value;

	public string ToString(string? format, IFormatProvider? formatProvider)
		=> ((float)this).ToString(format, formatProvider);

	public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		=> ((float)this).TryFormat(destination, out charsWritten, format, provider);
}
