using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Represents a lighting effect.</summary>
/// <remarks>Some common effect properties are present on the type itself, in order to avoid the overhead that would be associated with extended property values</remarks>
[DataContract]
public sealed class LightingEffect
{
	/// <summary>ID of the effect.</summary>
	[DataMember(Order = 1)]
	public required Guid EffectId { get; init; }

	/// <summary>Main color of the effect, if applicable.</summary>
	/// <remarks>This property is applicable to all effects having a property named <c>Color</c> that can be represented as a 32 bit unsigned integer.</remarks>
	[DataMember(Order = 2)]
	public uint Color { get; init; }

	/// <summary>Speed of the effect.</summary>
	/// <remarks>This property is applicable to all effects having a property named <c>Speed</c> that can be represented as a 32 bit unsigned integer.</remarks>
	[DataMember(Order = 3)]
	public uint Speed { get; init; }

	/// <summary>Values for all properties that are not present on this object.</summary>
	[DataMember(Order = 4)]
	public required ImmutableArray<PropertyValue> ExtendedPropertyValues { get; init; } = [];
}
