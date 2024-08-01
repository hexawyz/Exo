using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Represents the value of a property.</summary>
/// <remarks>
/// <para>
/// This structure does not indicate the <see cref="DataType"/> used for the property.
/// This data type needs to be retrieved from the metadata separately.
/// </para>
/// <para>
/// This structure provides protobuf-like serialization of values, where properties are addressed by a predefined index, and the schema is defined separately.
/// This is necessary, since serializing random non-predefined types through protobuf is not possible unless wrapping the data in a byte array.
/// Wrapping serialized (protobuf, json, etc.) data in a byte array would work, but we still need to transfer the property metadata, so it would not change much.
/// Having things done that way means that for the time being, the frontend does not need to load any external type.
/// NB: The frontend having to load external types is not a forbidden scenario, but limiting the number of assemblies loaded is better for performance,
/// and allows for better separation of concerns.
/// </para>
/// </remarks>
[DataContract]
public readonly struct PropertyValue
{
	/// <summary>The index of the property.</summary>
	[DataMember(Order = 1)]
	public required uint Index { get; init; }

	private readonly DataValue _value;

	/// <summary>The value of the property.</summary>
	[DataMember(Order = 2)]
	public required DataValue Value
	{
		get => _value;
		init => _value = value;
	}

	public static ref readonly DataValue GetValueRef(ref readonly PropertyValue propertyValue) => ref propertyValue._value;
}
