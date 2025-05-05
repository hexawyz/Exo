using System.Runtime.CompilerServices;

namespace Exo.Lighting.Effects;

public interface ILightingEffect
{
	// Quite a bit of a complex gymnastic, but these methods support the effect serialization framework by allowing direct serialization of an effect instance of unknown type.
	// Implementation must be identical to what is implemented by static members of ILightingEffect<TEffect>. However these allow accessing this from an untyped instance.
	Guid GetEffectId();
	bool TryGetSize(out uint size);
	void Serialize(ref BufferWriter writer);

	// These have a default implementation to workaround the stupid CS8920 error that prevents using the interface in any regular type like TaskCompletionSource or List.
	// In practice we should never have any real use case where these are not properly implemented, but it will become easier to miss.
	static virtual Guid EffectId => throw new NotImplementedException();
	static virtual LightingEffectInformation GetEffectMetadata() => throw new NotImplementedException();
}

public interface ILightingEffect<TSelf> : ILightingEffect, ISerializer<TSelf>
	where TSelf : struct, ILightingEffect<TSelf>
{
	Guid ILightingEffect.GetEffectId() => TSelf.EffectId;
	bool ILightingEffect.TryGetSize(out uint size) => TSelf.TryGetSize(in Unsafe.Unbox<TSelf>(this), out size);
	void ILightingEffect.Serialize(ref BufferWriter writer) => TSelf.Serialize(ref writer, in Unsafe.Unbox<TSelf>(this));
}

public interface ISingletonLightingEffect : ILightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	static abstract ISingletonLightingEffect SharedInstance { get; }
}

/// <summary>An interface indicating availability of a conversion from an other effect type.</summary>
/// <remarks>
/// <para>
/// Effect conversions should ONLY be provided for effects that are a superset of the original effect.
/// This will allow converting "generic", feature-limited effects into device-specific, feature-full effects.
/// </para>
/// <para>
/// One of the goal of this feature is to not clutter the effect list for a lighting zone with redundant effects.
/// It should be possible to set basic effects via API for operations such as "set everything to green", and this conversion mechanism will support that.
/// </para>
/// </remarks>
/// <typeparam name="TSourceEffect">The effect type that represents a feature-subset of the current effect type.</typeparam>
public interface IConvertibleLightingEffect<TSourceEffect, TSelf> : ILightingEffect<TSelf>
	where TSourceEffect : struct, ILightingEffect<TSourceEffect>
	where TSelf : struct, IConvertibleLightingEffect<TSourceEffect, TSelf>
{
	public static abstract implicit operator TSelf(in TSourceEffect effect);
}
