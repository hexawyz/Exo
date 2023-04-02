using System;
using System.Collections.Immutable;
using Exo.Lighting.Effects;

namespace Exo.Lighting;

public interface ILightZone
{
	ImmutableArray<Type> SupportedSpecificLightEffects { get; }

	SupportedWellKnownLightEffects SupportedLightEffects { get; }

	int AddressableLightCount { get; }

	bool CanAddressLights(WellKnownLightEffect effect);

	void SetLight(int lightIndex, RgbColor color);

	bool SupportsEffect<T>();

	void SetLightEffect<T>(T effect)
		where T : IColorEffect;

	T CreateApplicableEffect<T>()
		where T : IApplicableColorEffect;
}
