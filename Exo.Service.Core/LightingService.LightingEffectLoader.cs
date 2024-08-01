namespace Exo.Service;

public sealed partial class LightingService
{
	// Helper to load effects for a device arrival.
	// Here, we use a hash set to reduce the number of calls to the effect serializer, assuming it would be slightly faster than otherwise.
	// We could do without it without technical problem, though.
	private readonly struct LightingEffectLoader
	{
		private readonly HashSet<Type> _loadedTypes = new();

		public LightingEffectLoader() { }

		public void RegisterEffects(Type[] effectTypes)
		{
			foreach (var effectType in effectTypes)
			{
				if (_loadedTypes.Add(effectType))
				{
					_ = EffectSerializer.GetEffectInformation(effectType);
				}
			}
		}
	}
}

