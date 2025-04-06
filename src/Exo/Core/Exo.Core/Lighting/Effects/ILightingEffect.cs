using Exo.Contracts;

namespace Exo.Lighting.Effects;

public interface ILightingEffect { }

public interface ILightingEffect<TEffect> : ISerializer<TEffect>
	where TEffect : struct, ILightingEffect<TEffect>
{
	static abstract LightingEffectInformation GetEffectMetadata();
}

public interface ISingletonLightingEffect : ILightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	static abstract ISingletonLightingEffect SharedInstance { get; }
}
