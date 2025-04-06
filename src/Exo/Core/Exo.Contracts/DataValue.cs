using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>This is used to provide values for non-deterministically typed data.</summary>
/// <remarks>
/// <para>
/// The actual data type will depend on a <see cref="DataType"/> value provided separately.
/// Instances of <see cref="DataValue"/> do not expose a field for each possible <see cref="DataType"/>, because many value can share the same underlying representation.
/// Instead, the values exposed here represent various possible protobuf representations.
/// </para>
/// <para>
/// When possible, values are stored internally using their most compact binary form.
/// The internal storage format is not meant to be used as an exchange format, but it provides a convenient way to represent many values in a non-boxed way, and be able to serialize them into an optimal protobuf encoding.
/// This struct will have a constant size of 24 bytes on x64, which is way larger than most datatypes need, but will also save on the number of objects allocated.
/// It is also better than a naive implementation with a backing field for each possible kind of value.
/// </para>
/// </remarks>
[DataContract]
[StructLayout(LayoutKind.Sequential)]
public readonly struct DataValue : IEquatable<DataValue>
{
	private sealed class DataType
	{
		private readonly int _value;

		public DataType(int value) => _value = value;

		public override int GetHashCode() => _value;
	}

	private static readonly object EmptyValueSentinel = new DataType(0);
	private static readonly object UnsignedValueSentinel = new DataType(1);
	private static readonly object SignedValueSentinel = new DataType(2);
	private static readonly object SingleValueSentinel = new DataType(3);
	private static readonly object DoubleValueSentinel = new DataType(4);
	private static readonly object GuidValueSentinel = new DataType(5);

	private readonly object? _valueOrDataType;
	private readonly ulong _data0;
	private readonly ulong _data1;

	public DataValue() => _valueOrDataType = EmptyValueSentinel;

	public bool IsDefault => _valueOrDataType is null;

	// The main purpose of having this member is to force empty values to be serialized.
	[DataMember(Order = 1)]
	public bool IsEmpty
	{
		get => _valueOrDataType == EmptyValueSentinel;
		init
		{
			if (value)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
		}
	}

	[DataMember(Order = 2)]
	public ulong UnsignedValue
	{
		get => _valueOrDataType == UnsignedValueSentinel ? _data0 : 0;
		init
		{
			if (value == 0)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = UnsignedValueSentinel;
				_data0 = value;
			}
		}
	}

	[DataMember(Order = 3)]
	public long SignedValue
	{
		get => _valueOrDataType == SignedValueSentinel ? (long)_data0 : 0;
		init
		{
			if (value == 0)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = SignedValueSentinel;
				_data0 = (ulong)value;
			}
		}
	}

	[DataMember(Order = 4)]
	public float SingleValue
	{
		get => _valueOrDataType == SingleValueSentinel ? Unsafe.BitCast<uint, float>((uint)_data0) : 0;
		init
		{
			if (value == 0)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = SingleValueSentinel;
				_data0 = Unsafe.BitCast<float, uint>(value);
			}
		}
	}

	[DataMember(Order = 5)]
	public double DoubleValue
	{
		get => _valueOrDataType == DoubleValueSentinel ? Unsafe.BitCast<ulong, double>(_data0) : 0;
		init
		{
			if (value == 0)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = DoubleValueSentinel;
				_data0 = Unsafe.BitCast<double, ulong>(value);
			}
		}
	}

	[DataMember(Order = 6)]
	public byte[]? BytesValue
	{
		get => _valueOrDataType as byte[];
		init
		{
			if (value is null)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = value;
			}
		}
	}

	[DataMember(Order = 7)]
	public string? StringValue
	{
		get => _valueOrDataType as string;
		init
		{
			if (value is null)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = value;
			}
		}
	}

	[DataMember(Order = 8)]
	public Guid GuidValue
	{
		get => _valueOrDataType == GuidValueSentinel ? Unsafe.As<ulong, Guid>(ref Unsafe.AsRef(in _data0)) : default;
		init
		{
			if (value == default)
			{
				_valueOrDataType = EmptyValueSentinel;
			}
			else
			{
				_valueOrDataType = GuidValueSentinel;
				Unsafe.As<ulong, Guid>(ref Unsafe.AsRef(in _data0)) = value;
			}
		}
	}

	public override bool Equals(object? obj) => obj is DataValue value && Equals(value);
	public bool Equals(DataValue other) => ReferenceEquals(_valueOrDataType, other._valueOrDataType) && _data0 == other._data0 && _data1 == other._data1;
	public override int GetHashCode() => HashCode.Combine(_valueOrDataType, _data0, _data1);

	public static bool operator ==(DataValue left, DataValue right) => left.Equals(right);
	public static bool operator !=(DataValue left, DataValue right) => !(left == right);

	public static implicit operator DataValue(sbyte value) => new() { SignedValue = value };
	public static implicit operator DataValue(byte value) => new() { UnsignedValue = value };

	public static implicit operator DataValue(short value) => new() { SignedValue = value };
	public static implicit operator DataValue(ushort value) => new() { UnsignedValue = value };

	public static implicit operator DataValue(int value) => new() { SignedValue = value };
	public static implicit operator DataValue(uint value) => new() { UnsignedValue = value };

	public static implicit operator DataValue(long value) => new() { SignedValue = value };
	public static implicit operator DataValue(ulong value) => new() { UnsignedValue = value };

	public static implicit operator DataValue(Half value) => new() { SingleValue = (float)value };
	public static implicit operator DataValue(float value) => new() { SingleValue = value };
	public static implicit operator DataValue(double value) => new() { DoubleValue = value };

	public static implicit operator DataValue(Guid value) => new() { GuidValue = value };
}
