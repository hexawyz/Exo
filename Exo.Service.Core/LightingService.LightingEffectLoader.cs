namespace Exo.Service;

#pragma warning disable IDE0040 // Add accessibility modifiers
partial class LightingService
#pragma warning restore IDE0040 // Add accessibility modifiers
{
	// Helper to load effects for a device arrival.
	// Here, we use a hash set to reduce the number of calls to the effect serializer, assuming it would be slightly faster than otherwise.
	// We could do without it without technical problem, though.
	private readonly struct LightingEffectLoader
	{
		private readonly HashSet<Type> _loadedTypes = new();
		private readonly LightingEffectMetadataService _lightingEffectMetadataService;

		public LightingEffectLoader(LightingEffectMetadataService lightingEffectMetadataService)
		{
			_lightingEffectMetadataService = lightingEffectMetadataService;
		}

		public void RegisterEffects(Type[] effectTypes)
		{
			foreach (var effectType in effectTypes)
			{
				if (_loadedTypes.Add(effectType))
				{
					_lightingEffectMetadataService.RegisterEffect(effectType);
				}
			}
		}
	}
}

