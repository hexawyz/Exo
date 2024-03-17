using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Information on a property that can be configured through the UI.</summary>
[DataContract]
public sealed class ConfigurablePropertyInformation
{
	/// <summary>The index to use for serializing the property.</summary>
	/// <remarks>This value should be unspecified for properties that would have a direct predefined mapping.</remarks>
	[DataMember(Order = 1)]
	public required uint? Index { get; init; }

	/// <summary>The name of the property.</summary>
	[DataMember(Order = 2)]
	public required string Name { get; init; }

	// TODO: Replace all display name & similar stuff everywhere by a GUID that will be used for localization. Also, properties should probably have a display order.

	/// <summary>The display name of the property.</summary>
	[DataMember(Order = 3)]
	public required string DisplayName { get; init; }

	/// <summary>The description of the property.</summary>
	[DataMember(Order = 4)]
	public string? Description { get; init; }

	/// <summary>The data type of the property.</summary>
	[DataMember(Order = 5)]
	public required DataType DataType { get; init; }

	/// <summary>The default value of the property, if any.</summary>
	[DataMember(Order = 6)]
	public DataValue? DefaultValue { get; init; }

	/// <summary>The minimum value of the property, if applicable.</summary>
	[DataMember(Order = 7)]
	public DataValue? MinimumValue { get; init; }

	/// <summary>The maximum value of the property, if applicable.</summary>
	[DataMember(Order = 8)]
	public DataValue? MaximumValue { get; init; }

	/// <summary>The unit in which numeric values are expressed, if applicable.</summary>
	[DataMember(Order = 9)]
	public string? Unit { get; init; }

	/// <summary>Determines the allowed values if the field is an enumeration.</summary>
	// NB: Might not be very efficient to have this here, since it would be replicated for every property if the same type is reused. Let's see how critical this is later.
	[DataMember(Order = 10)]
	public required ImmutableArray<EnumerationValue> EnumerationValues { get; init; }

	/// <summary>The number of elements in the array, for fixed-length array data types.</summary>
	/// <remarks>Fixed-length arrays are the only kind of array supported. The array data will be materialized into <see cref="DataValue.BytesValue"/>.</remarks>
	[DataMember(Order = 11)]
	public int? ArrayLength { get; init; }
}
