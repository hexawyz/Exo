using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Exo.Features;
using Exo.Features.Lighting;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcLightingService : ILightingService
{
	private readonly LightingService _lightingService;
	private readonly ILogger<GrpcLightingService> _logger;

	public GrpcLightingService(ILogger<GrpcLightingService> logger, LightingService lightingService)
	{
		_logger = logger;
		_lightingService = lightingService;
	}

	// TODO: Refactor the lighting service and remove the raw device-related stuff.
	// The remove notifications are also kind of a duplicate with device notifications, so they could maybe be removed.
	// Maybe simply having a GetLightingDeviceCapabilities call would be enough. => Let's have a simpler watcher instead. Only push device capabilities.
	public async IAsyncEnumerable<Contracts.Ui.Settings.LightingDeviceInformation> WatchLightingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcLightingServiceDeviceWatchStart();
		try
		{
			await foreach (var notification in _lightingService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
			{
				if (notification.Kind is WatchNotificationKind.Removal) continue;

				// TODO: This should only send updates when stuff has changed.
				// Rework still needs to be done on the base service to properly handle persistance.

				LightingBrightnessCapabilities? brightnessCapabilities = null;
				LightingPaletteCapabilities? paletteCapabilities = null;

				RegisterEffectTypes(notification);

				var lightingFeatures = notification.Driver!.GetFeatureSet<ILightingDeviceFeature>();

				if (lightingFeatures.GetFeature<ILightingBrightnessFeature>() is { } brightnessFeature)
				{
					brightnessCapabilities = new() { MinimumBrightness = brightnessFeature.MinimumBrightness, MaximumBrightness = brightnessFeature.MaximumBrightness };
				}

				yield return new()
				{
					DeviceId = notification.DeviceInformation.Id,
					BrightnessCapabilities = brightnessCapabilities,
					PaletteCapabilities = paletteCapabilities,
					UnifiedLightingZone = notification.LightingDeviceInformation.UnifiedLightingZone?.ToGrpc(),
					LightingZones = ImmutableArray.CreateRange(notification.LightingDeviceInformation.LightingZones, z => z.ToGrpc()),
				};
			}
		}
		finally
		{
			_logger.GrpcLightingServiceDeviceWatchStop();
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
			try
			{
				_ = EffectSerializer.GetEffectInformation(effectType);
			}
			catch (Exception ex)
			{
				_logger.GrpcLightingServiceEffectInformationRetrievalError(effectType, ex);
			}
		}
	}

	public async ValueTask ApplyDeviceLightingChangesAsync(DeviceLightingUpdate update, CancellationToken cancellationToken)
	{
		if (update.BrightnessLevel != 0)
		{
			_lightingService.SetBrightness(update.DeviceId, update.BrightnessLevel);
		}

		foreach (var ze in update.ZoneEffects)
		{
			try
			{
				EffectSerializer.DeserializeAndSet(_lightingService, update.DeviceId, ze.ZoneId, ze.Effect!);
			}
			catch (Exception ex)
			{
				_logger.GrpcLightingServiceEffectApplicationError(update.DeviceId, ze.Effect?.EffectId ?? default, ze.ZoneId, ex);
			}
		}

		await _lightingService.ApplyChanges(update.DeviceId);
	}

	public async ValueTask ApplyMultiDeviceLightingChangesAsync(MultiDeviceLightingUpdates updates, CancellationToken cancellationToken)
	{
		foreach (var update in updates.DeviceUpdates)
		{
			if (update.BrightnessLevel != 0)
			{
				_lightingService.SetBrightness(update.DeviceId, update.BrightnessLevel);
			}

			foreach (var ze in update.ZoneEffects)
			{
				try
				{
					EffectSerializer.DeserializeAndSet(_lightingService, update.DeviceId, ze.ZoneId, ze.Effect!);
				}
				catch (Exception ex)
				{
					_logger.GrpcLightingServiceEffectApplicationError(update.DeviceId, ze.Effect?.EffectId ?? default, ze.ZoneId, ex);
				}
			}
		}

		foreach (var update in updates.DeviceUpdates)
		{
			await _lightingService.ApplyChanges(update.DeviceId).ConfigureAwait(false);
		}
	}

	public ValueTask<LightingEffectInformation> GetEffectInformationAsync(EffectTypeReference typeReference, CancellationToken cancellationToken)
		=> new(EffectSerializer.GetEffectInformation(typeReference.TypeId));

	public async IAsyncEnumerable<DeviceZoneLightingEffect> WatchEffectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcLightingServiceEffectWatchStart();
		try
		{
			await foreach (var notification in _lightingService.WatchEffectsAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return new()
				{
					DeviceId = notification.DeviceId,
					ZoneId = notification.ZoneId,
					Effect = notification.SerializeEffect(),
				};
			}
		}
		finally
		{
			_logger.GrpcLightingServiceEffectWatchStop();
		}
	}

	public async IAsyncEnumerable<DeviceBrightnessLevel> WatchBrightnessAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcLightingServiceBrightnessWatchStart();
		try
		{
			await foreach (var notification in _lightingService.WatchBrightnessAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return new()
				{
					DeviceId = notification.DeviceId,
					BrightnessLevel = notification.BrightnessLevel,
				};
			}
		}
		finally
		{
			_logger.GrpcLightingServiceBrightnessWatchStop();
		}
	}
}
