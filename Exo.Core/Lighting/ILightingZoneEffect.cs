using Exo.Lighting.Effects;

namespace Exo.Lighting;

/// <summary>A light zone supporting a specific lighting effect.</summary>
/// <remarks>
/// <para>
/// Most lighting effects are simple in nature, and can be materialized by the effect name and a few well-known parameters.
/// The effect name being represented by <typeparamref name="TEffect"/>, and the parameters being defined inside the readonly structure associated with the parameter.
/// </para>
/// <para>
/// A dynamic lighting effect, such as continuously (manually) updating adressable RGB led zones, would typically be considered an advanced effect,
/// as it requires tighter interaction with the lighting zone.
/// </para>
/// </remarks>
/// <typeparam name="TEffect"></typeparam>
public interface ILightingZoneEffect<TEffect>
	where TEffect : struct, ILightingEffect
{
	/// <summary>Applies the effect to the lighting zone with the specified parameters.</summary>
	/// <param name="effect">The effect parameters to apply.</param>
	void ApplyEffect(in TEffect effect);

	/// <summary>Gets the effect currently applied to the lighting zone if it is of type <typeparamref name="TEffect"/>.</summary>
	/// <param name="effect">The effect currently applied to the lighting zone, or default value if not found.</param>
	/// <returns>
	/// <see langword="true"/> if the current effect was of type <typeparamref name="TEffect"/> and the parameters were returned in <paramref name="effect"/>; otherwise <see langword="false"/>
	/// </returns>
	bool TryGetCurrentEffect(out TEffect effect);
}
