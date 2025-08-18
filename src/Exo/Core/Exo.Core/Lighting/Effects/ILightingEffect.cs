using System.Collections.Immutable;
using System.ComponentModel;
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

// Effect infrastructure support.
[EditorBrowsable(EditorBrowsableState.Never)]
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

// TODO: Dynamic effect API.
/// <summary>Represents a lighting effect that supports addressable lighting zones.</summary>
/// <remarks>
/// <para>
/// Addressable effects are either programmed or dynamic and must provide at least one of those implementations.
/// </para>
/// <para>
/// Programmed effects can always be automatically interpreted as dynamic effects, but the opposite is not true.
/// However, some effects will benefit from providing both implementations as their dynamic implementation will be cheaper.
/// </para>
/// </remarks>
public interface IAddressableLightingEffect : ILightingEffect
{
	/// <summary>Indicates if frames generated for a large size can be used for any smaller size.</summary>
	/// <remarks>
	/// <para>
	/// The rendering of many effects will be relatively independent of the frame size.
	/// In that case, the same set of frames could be reused for multiple zones, avoiding unnecessary allocations.
	/// </para>
	/// </remarks>
	static abstract bool CanUseLargerFramesForSmallerSizes { get; }
}

/// <summary>Represents a lighting effect that can be used for addressable lighting.</summary>
/// <typeparam name="TColor">The type of color items supported by the lighting effect.</typeparam>
public interface IProgrammableLightingEffect<TColor> : IAddressableLightingEffect
	where TColor : unmanaged
{
	/// <summary>Gets the frames that can be used to configure the effect on a device.</summary>
	/// <param name="ledCount">The number of LEDs to include.</param>
	/// <param name="capacity">The maximum number of frames expected.</param>
	/// <returns>Frames to be used to configure or run the effect on a device.</returns>
	ImmutableArray<LightingEffectFrame<TColor>> GetEffectFrames(int ledCount, int capacity);
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
