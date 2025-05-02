using System.Collections.Immutable;

namespace Exo.Lighting;

public sealed class LightingEffectInformation : IEquatable<LightingEffectInformation?>
{
	/// <summary>ID of the effect.</summary>
	/// <remarks>
	/// This is the effect type ID, mandatory for all effect types.
	/// It is used as an unique reference to the effect type, and as a key for UI localization.
	/// </remarks>
	public required Guid EffectId { get; init; }

	private readonly ImmutableArray<ConfigurablePropertyInformation> _properties = [];

	/// <summary>Gets the properties of the lighting effect.</summary>
	/// <remarks>
	/// <para>
	/// This information is necessary to build the UI used to configure effects.
	/// Effects can only rely on very specific data types, whose underlying representation is compatible with <see cref="DataValue"/>.
	/// </para>
	/// <para>
	/// All configurable properties of the effect will be listed here, including those that are exposed as intrinsic properties of <see cref="LightingEffect"/>.
	/// Matching of properties that are exposed as an intrinsic is done based on name and type.
	/// </para>
	/// </remarks>
	public required ImmutableArray<ConfigurablePropertyInformation> Properties
	{
		get => _properties;
		init => _properties = value.IsDefaultOrEmpty ? [] : value;
	}

	public override bool Equals(object? obj) => Equals(obj as LightingEffectInformation);

	public bool Equals(LightingEffectInformation? other)
		=> other is not null &&
			EffectId == other.EffectId &&
			Properties.SequenceEqual(other.Properties);

	public override int GetHashCode() => HashCode.Combine(EffectId, Properties.Length);

	public static bool operator ==(LightingEffectInformation? left, LightingEffectInformation? right) => EqualityComparer<LightingEffectInformation>.Default.Equals(left, right);
	public static bool operator !=(LightingEffectInformation? left, LightingEffectInformation? right) => !(left == right);
}
