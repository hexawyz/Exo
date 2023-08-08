namespace Exo.Lighting.Effects;

public interface ILightingEffect { }

public interface ISingletonLightingEffect : ILightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	static abstract ISingletonLightingEffect SharedInstance { get; }
}
