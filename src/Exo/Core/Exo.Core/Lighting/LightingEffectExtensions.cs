using Exo.Lighting.Effects;

namespace Exo.Lighting;

public static class LightingEffectExtensions
{
	public static bool TryGetEffect<TEffect>(this ILightingEffect effect, out TEffect? value)
		where TEffect : ILightingEffect
	{
		if (effect is TEffect e)
		{
			value = e;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}
}
