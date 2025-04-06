using System.Runtime.CompilerServices;
using Exo.Contracts;

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

public interface ILightingEffect<TEffect> : ILightingEffect, ISerializer<TEffect>
	where TEffect : struct, ILightingEffect<TEffect>
{
	Guid ILightingEffect.GetEffectId() => TEffect.EffectId;
	bool ILightingEffect.TryGetSize(out uint size) => TEffect.TryGetSize(in Unsafe.Unbox<TEffect>(this), out size);
	void ILightingEffect.Serialize(ref BufferWriter writer) => TEffect.Serialize(ref writer, in Unsafe.Unbox<TEffect>(this));
}

public interface ISingletonLightingEffect : ILightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	static abstract ISingletonLightingEffect SharedInstance { get; }
}
