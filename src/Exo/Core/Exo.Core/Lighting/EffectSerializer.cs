using System.ComponentModel;
using Exo.Lighting.Effects;

namespace Exo.Lighting;

// To replace the current implementation
public static class FutureEffectSerializer
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void RegisterEffect<TEffect>()
		where TEffect : struct, ILightingEffect<TEffect>
	{
	}

	public static void SetEffect(ILightingZone lightingZone, ReadOnlySpan<byte> data)
	{
	}
}
