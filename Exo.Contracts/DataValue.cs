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
public readonly struct DataValue
{
	private static readonly object UnknownValueSentinel = new();
	private static readonly object UnsignedValueSentinel = new();
	private static readonly object SignedValueSentinel = new();
	private static readonly object SingleValueSentinel = new();
	private static readonly object DoubleValueSentinel = new();
	private static readonly object GuidValueSentinel = new();

	private readonly object? _valueOrDataType;
	private readonly ulong _data0;
	private readonly ulong _data1;

	public DataValue() => _valueOrDataType = UnknownValueSentinel;

	public bool IsDefault => _valueOrDataType is null;

	[DataMember(Order = 1)]
	public ulong UnsignedValue
	{
		get => _valueOrDataType == UnsignedValueSentinel ? _data0 : 0;
		init
		{
			_valueOrDataType = UnsignedValueSentinel;
			_data0 = value;
		}
	}

	[DataMember(Order = 2)]
	public long SignedValue
	{
		get => _valueOrDataType == SignedValueSentinel ? (long)_data0 : 0;
		init
		{
			_valueOrDataType = SignedValueSentinel;
			_data0 = (ulong)value;
		}
	}

	[DataMember(Order = 3)]
	public float SingleValue
	{
		get => _valueOrDataType == SingleValueSentinel ? Unsafe.BitCast<uint, float>((uint)_data0) : 0;
		init
		{
			_valueOrDataType = SingleValueSentinel;
			_data0 = Unsafe.BitCast<float, uint>(value);
		}
	}

	[DataMember(Order = 4)]
	public double DoubleValue
	{
		get => _valueOrDataType == DoubleValueSentinel ? Unsafe.BitCast<ulong, double>(_data0) : 0;
		init
		{
			_valueOrDataType = DoubleValueSentinel;
			_data0 = Unsafe.BitCast<double, ulong>(value);
		}
	}

	[DataMember(Order = 5)]
	public byte[]? BytesValue
	{
		get => _valueOrDataType as byte[];
		init => _valueOrDataType = value;
	}

	[DataMember(Order = 6)]
	public string? StringValue
	{
		get => _valueOrDataType as string;
		init => _valueOrDataType = value;
	}

	[DataMember(Order = 7)]
	public Guid GuidValue
	{
		get => _valueOrDataType == GuidValueSentinel ? Unsafe.As<ulong, Guid>(ref Unsafe.AsRef(in _data0)) : default;
		init
		{
			_valueOrDataType = GuidValueSentinel;
			Unsafe.As<ulong, Guid>(ref Unsafe.AsRef(in _data0)) = value;
		}
	}
}
