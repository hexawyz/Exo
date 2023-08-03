using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeviceTools;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcLightingService : ILightingService
{
	private readonly LightingService _lightingService;

	public GrpcLightingService(LightingService lightingService) => _lightingService = lightingService;

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.LightingDeviceInformation>> WatchLightingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _lightingService.WatchDevicesAsync(cancellationToken))
		{
			RegisterEffectTypes(notification);

			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = new()
				{
					DeviceInformation = notification.DeviceInformation.ToGrpc(),
					UnifiedLightingZone = notification.LightingDeviceInformation.UnifiedLightingZone?.ToGrpc(),
					LightingZones = Array.ConvertAll(notification.LightingDeviceInformation.LightingZones.AsMutable(), z => z.ToGrpc()),
				},
			};
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
			_ = GrpcEffectSerializer.GetEffectInformation(effectType);
		}
	}

	public async ValueTask ApplyDeviceLightingEffectsAsync(DeviceLightingEffects effects, CancellationToken cancellationToken)
	{
		foreach (var ze in effects.ZoneEffects)
		{
			GrpcEffectSerializer.DeserializeAndSet(_lightingService, effects.DeviceId, ze.ZoneId, ze.Effect!);
		}

		await _lightingService.ApplyChanges(effects.DeviceId);
	}

	public ValueTask ApplyMultipleDeviceLightingEffectsAsync(MultipleDeviceLightingEffects effects, CancellationToken cancellationToken) => throw new NotImplementedException();

	public ValueTask<LightingEffectInformation> GetEffectInformationAsync(EffectTypeReference typeReference, CancellationToken cancellationToken)
		=> new(GrpcEffectSerializer.GetEffectInformation(typeReference.TypeName));

	public async IAsyncEnumerable<DeviceZoneLightingEffect> WatchEffectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _lightingService.WatchEffectsAsync(cancellationToken))
		{
			yield return new()
			{
				DeviceId = notification.DeviceId,
				ZoneId = notification.ZoneId,
				Effect = GrpcEffectSerializer.Serialize(notification.Effect),
			};
		}
	}
}
