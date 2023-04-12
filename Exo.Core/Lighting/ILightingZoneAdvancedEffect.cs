using Exo.Lighting.Effects;

namespace Exo.Lighting;

/// <summary>A light zone supporting an advanced lighting effect.</summary>
/// <remarks>
/// Advanced lighting effects necessitate stronger connection to the lighting zone and require special instantiation logic.
/// This will typically be the case of addressable RGB lighting effects, which will require updating the lighting frequently.
/// </remarks>
/// <typeparam name="TEffect"></typeparam>
public interface ILightingZoneAdvancedEffect<TEffect>
	where TEffect : ILightingEffect
{
	/// <summary>Creates a new instance of <see cref="TEffect"/> that can be used to parameterize the effect, and apply it immediately.</summary>
	/// <returns>An instance of <see cref="TEffect"/> that is now applied to the lighting zone.</returns>
	TEffect CreateAndApplyEffect();

	/// <summary>Gets the effect currently applied to the lighting zone if it is of type <typeparamref name="TEffect"/>.</summary>
	/// <param name="effect">The effect currently applied to the lighting zone, or default value if not found.</param>
	/// <returns>
	/// <see langword="true"/> if the current effect was of type <typeparamref name="TEffect"/> and the parameters were returned in <paramref name="effect"/>; otherwise <see langword="false"/>
	/// </returns>
	bool TryGetCurrentEffect(out TEffect effect);
}
