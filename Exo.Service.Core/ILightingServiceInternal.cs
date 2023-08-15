using System;
using Exo.Contracts;
using Exo.Lighting.Effects;

namespace Exo.Service;

internal interface ILightingServiceInternal
{
	void SetEffect<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect, LightingEffect serializedEffect, bool isRestore)
		where TEffect : struct, ILightingEffect;
}
