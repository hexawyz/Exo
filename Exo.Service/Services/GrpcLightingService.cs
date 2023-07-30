using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcLightingService : ILightingService
{
	private readonly LightingService _lightingService;
	// TODO: Remove and let the serializer do everything earlier?
	private readonly ConcurrentDictionary<string, WeakReference<Type>> _effectTypeDictionary = new();

	public GrpcLightingService(LightingService lightingService) => _lightingService = lightingService;

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.LightingDeviceInformation>> WatchLightingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _lightingService.WatchDevicesAsync(cancellationToken))
		{
			RegisterEffectTypes(notification);

			yield return new WatchNotification<Ui.Contracts.LightingDeviceInformation>
			(
				notification.Kind.ToGrpc(),
				new()
				{
					DeviceInformation = notification.DeviceInformation.ToGrpc(),
					UnifiedLightingZone = notification.LightingDeviceInformation.UnifiedLightingZone?.ToGrpc(),
					LightingZones = Array.ConvertAll(notification.LightingDeviceInformation.LightingZones.AsMutable(), z => z.ToGrpc()),
				}
			);
		}
	}

	private void RegisterEffectTypes(LightingDeviceWatchNotification notification)
	{
		if (notification.LightingDeviceInformation.UnifiedLightingZone is { } unifiedLightingZone)
		{
			RegisterEffectTypes(unifiedLightingZone.SupportedEffectTypes);
		}
		foreach (var zone in notification.LightingDeviceInformation.LightingZones)
		{
			RegisterEffectTypes(zone.SupportedEffectTypes);
		}
	}

	private void RegisterEffectTypes(ImmutableArray<Type> supportedEffectTypes)
	{
		foreach (var effectType in supportedEffectTypes)
		{
			_ = _effectTypeDictionary.GetOrAdd(effectType.ToString(), (_, t) => new(t), effectType);
		}
	}

	public async ValueTask ApplyDeviceLightingEffectsAsync(DeviceLightingEffects effects, CancellationToken cancellationToken)
	{
		foreach (var ze in effects.ZoneEffects)
		{
			GrpcEffectSerializer.DeserializeAndSet(_lightingService, effects.UniqueId, ze.ZoneId, ze.Effect!);
		}

		await _lightingService.ApplyChanges(effects.UniqueId);
	}

	public ValueTask ApplyMultipleDeviceLightingEffectsAsync(MultipleDeviceLightingEffects effects, CancellationToken cancellationToken) => throw new NotImplementedException();

	public ValueTask<LightingEffectInformation> GetEffectInformationAsync(EffectTypeReference typeReference, CancellationToken cancellationToken)
	{
		if (!_effectTypeDictionary.TryGetValue(typeReference.TypeName, out var wr) || !wr.TryGetTarget(out var effectType))
		{
			throw new KeyNotFoundException("Information on the specified type was not found.");
		}
		return new(GrpcEffectSerializer.GetEffectInformation(effectType));
	}

	public ValueTask<DeviceLightingEffects> WatchEffectsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
