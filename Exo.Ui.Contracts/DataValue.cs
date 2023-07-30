using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

/// <summary>This is used to provide values for non-deterministically typed data.</summary>
/// <remarks>
/// The actual data type will depend on a <see cref="DataType"/> value provided separately.
/// Instances of <see cref="DataValue"/> do not expose a field for each possible <see cref="DataType"/>, because many value can share the same underlying representation.
/// For example, all unsigned integers are a subset of 64 bit unsigned integers.
/// </remarks>
[DataContract]
public sealed class DataValue
{
	[DataMember(Order = 1)]
	public ulong/*?*/ UnsignedValue { get; init; }
	[DataMember(Order = 2)]
	public long/*?*/ SignedValue { get; init; }
	[DataMember(Order = 3)]
	public float/*?*/ SingleValue { get; init; }
	[DataMember(Order = 4)]
	public double/*?*/ DoubleValue { get; init; }
	[DataMember(Order = 5)]
	public byte[]? BytesValue { get; init; }
	[DataMember(Order = 6)]
	public string? StringValue { get; init; }

	// Manually expose GUIDs as byte arrays instead of relying on protobuf internal format.
	public Guid/*?*/ GuidValue
	{
		get => BytesValue is not null ? new Guid(BytesValue) : default /*null*/;
		init => BytesValue = value/*?*/.ToByteArray();
	}
}
