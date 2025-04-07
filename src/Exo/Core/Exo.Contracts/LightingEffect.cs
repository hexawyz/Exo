using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Represents a lighting effect.</summary>
/// <remarks>Some common effect properties are present on the type itself, in order to avoid the overhead that would be associated with extended property values</remarks>
[DataContract]
[TypeId(0x04A72CE3, 0x07F1, 0x483E, 0xB4, 0x96, 0xB1, 0x2A, 0x45, 0x17, 0x79, 0x8D)]
public sealed class LightingEffect(Guid effectId, byte[] effectData) : IEquatable<LightingEffect?>
{
	// For protobuf…
	// TODO: Remove. This and the init accessors.
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public LightingEffect() : this(default, []) { }

	/// <summary>ID of the effect.</summary>
	[DataMember(Order = 1)]
	public Guid EffectId { get; init; } = effectId;

	/// <summary>Data of the effect</summary>
	[DataMember(Order = 2)]
	public byte[] EffectData { get; init; } = effectData;

	public static LightingEffect? FromRaw(byte[]? data)
	{
		if (data is null) return null;

		return new LightingEffect(new Guid(data.AsSpan(0, 16)), data[16..]);
	}

	public override bool Equals(object? obj) => Equals(obj as LightingEffect);

	public bool Equals(LightingEffect? other)
		=> other is not null &&
			EffectId.Equals(other.EffectId) &&
			(EffectData is null ? other.EffectData is null: other.EffectData is not null && EffectData.SequenceEqual(other.EffectData));

	public override int GetHashCode() => HashCode.Combine(EffectId, EffectData.Length);

	public static bool operator ==(LightingEffect? left, LightingEffect? right) => EqualityComparer<LightingEffect>.Default.Equals(left, right);
	public static bool operator !=(LightingEffect? left, LightingEffect? right) => !(left == right);
}
