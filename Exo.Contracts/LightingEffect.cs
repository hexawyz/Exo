using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Represents a lighting effect.</summary>
/// <remarks>Some common effect properties are present on the type itself, in order to avoid the overhead that would be associated with extended property values</remarks>
[DataContract]
[TypeId(0xFE4E94FA, 0xB60B, 0x4702, 0xB8, 0x4C, 0xE0, 0xA4, 0x68, 0x21, 0x93, 0xEA)]
public sealed class LightingEffect : IEquatable<LightingEffect?>
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

	private readonly ImmutableArray<PropertyValue> _extendedPropertyValues = [];

	/// <summary>Values for all properties that are not present on this object.</summary>
	[DataMember(Order = 4)]
	public required ImmutableArray<PropertyValue> ExtendedPropertyValues
	{
		get => _extendedPropertyValues;
		init => _extendedPropertyValues = value.NotNull();
	}

	public override bool Equals(object? obj) => Equals(obj as LightingEffect);

	public bool Equals(LightingEffect? other)
		=> other is not null &&
			EffectId.Equals(other.EffectId) &&
			Color == other.Color &&
			Speed == other.Speed &&
			ExtendedPropertyValues.SequenceEqual(other.ExtendedPropertyValues);

	public override int GetHashCode() => HashCode.Combine(EffectId, Color, Speed, ExtendedPropertyValues.Length);

	public static bool operator ==(LightingEffect? left, LightingEffect? right) => EqualityComparer<LightingEffect>.Default.Equals(left, right);
	public static bool operator !=(LightingEffect? left, LightingEffect? right) => !(left == right);
}
