using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.Primitives;

public readonly struct VariantNumber
{
	[SkipLocalsInit]
	public static VariantNumber Create<T>(T value)
		where T : unmanaged, INumber<T>
	{
		if (Unsafe.SizeOf<T>() > 16) throw new InvalidOperationException("The data type is too large.");
		var v = new VariantNumber();
		Unsafe.As<byte, T>(ref Unsafe.AsRef(in v._data0)) = value;
		return v;
	}

	[SkipLocalsInit]
	public static VariantNumber Create(ReadOnlySpan<byte> value)
	{
		if (value.Length > 16) throw new ArgumentException("The data is too large.");
		var v = new VariantNumber();
		value.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in v._data0), 16));
		return v;
	}

	public static T GetValue<T>(in VariantNumber value)
		where T : unmanaged, INumber<T>
	{
		if (Unsafe.SizeOf<T>() > 16) throw new InvalidOperationException("The data type is too large.");
		return Unsafe.As<byte, T>(ref Unsafe.AsRef(in value._data0));
	}

	public static ReadOnlySpan<byte> GetData(in VariantNumber value)
		=> MemoryMarshal.CreateReadOnlySpan(in value._data0, 16);

	private readonly byte _data0;
	private readonly byte _data1;
	private readonly byte _data2;
	private readonly byte _data3;
	private readonly byte _data4;
	private readonly byte _data5;
	private readonly byte _data6;
	private readonly byte _data7;
	private readonly byte _data8;
	private readonly byte _data9;
	private readonly byte _dataA;
	private readonly byte _dataB;
	private readonly byte _dataC;
	private readonly byte _dataD;
	private readonly byte _dataE;
	private readonly byte _dataF;

	public static implicit operator VariantNumber(byte value) => Create(value);
	public static implicit operator VariantNumber(ushort value) => Create(value);
	public static implicit operator VariantNumber(uint value) => Create(value);
	public static implicit operator VariantNumber(ulong value) => Create(value);
	public static implicit operator VariantNumber(UInt128 value) => Create(value);

	public static implicit operator VariantNumber(sbyte value) => Create(value);
	public static implicit operator VariantNumber(short value) => Create(value);
	public static implicit operator VariantNumber(int value) => Create(value);
	public static implicit operator VariantNumber(long value) => Create(value);
	public static implicit operator VariantNumber(Int128 value) => Create(value);

	public static implicit operator VariantNumber(Half value) => Create(value);
	public static implicit operator VariantNumber(float value) => Create(value);
	public static implicit operator VariantNumber(double value) => Create(value);

	public static explicit operator byte(in VariantNumber value) => value._data0;
	public static explicit operator ushort(in VariantNumber value) => GetValue<ushort>(in value);
	public static explicit operator uint(in VariantNumber value) => GetValue<uint>(in value);
	public static explicit operator ulong(in VariantNumber value) => GetValue<ulong>(in value);
	public static explicit operator UInt128(in VariantNumber value) => GetValue<UInt128>(in value);

	public static explicit operator sbyte(in VariantNumber value) => (sbyte)value._data0;
	public static explicit operator short(in VariantNumber value) => GetValue<short>(in value);
	public static explicit operator int(in VariantNumber value) => GetValue<int>(in value);
	public static explicit operator long(in VariantNumber value) => GetValue<long>(in value);
	public static explicit operator Int128(in VariantNumber value) => GetValue<Int128>(in value);

	public static explicit operator Half(in VariantNumber value) => GetValue<Half>(in value);
	public static explicit operator float(in VariantNumber value) => GetValue<float>(in value);
	public static explicit operator double(in VariantNumber value) => GetValue<double>(in value);
}
