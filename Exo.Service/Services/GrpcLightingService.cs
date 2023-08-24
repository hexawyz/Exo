using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcLightingService : ILightingService
{
	private readonly LightingService _lightingService;

	public GrpcLightingService(LightingService lightingService) => _lightingService = lightingService;

	// TODO: Refactor the lighting service and remove the raw device-related stuff.
	// The remove notifications are also kind of a duplicate with device notifications, so they could maybe be removed.
	// Maybe simply having a GetLightingDeviceCapabilities call would be enough.
	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.LightingDeviceInformation>> WatchLightingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _lightingService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
		{
			LightingBrightnessCapabilities? brightnessCapabilities = null;
			LightingPaletteCapabilities? paletteCapabilities = null;

			if (notification.Kind is not WatchNotificationKind.Removal)
			{
				RegisterEffectTypes(notification);

				var lightingDriver = (IDeviceDriver<ILightingDeviceFeature>)notification.Driver!;

				if (lightingDriver.Features.GetFeature<ILightingBrightnessFeature>() is { } brightnessFeature)
				{
					brightnessCapabilities = new() { MinimumBrightness = brightnessFeature.MinimumBrightness, MaximumBrightness = brightnessFeature.MaximumBrightness };
				}
			}

			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = new()
				{
					DeviceInformation = notification.DeviceInformation.ToGrpc(),
					BrightnessCapabilities = brightnessCapabilities,
					PaletteCapabilities = paletteCapabilities,
					UnifiedLightingZone = notification.LightingDeviceInformation.UnifiedLightingZone?.ToGrpc(),
					LightingZones = ImmutableArray.CreateRange(notification.LightingDeviceInformation.LightingZones, z => z.ToGrpc()),
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
			_ = EffectSerializer.GetEffectInformation(effectType);
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
			EffectSerializer.DeserializeAndSet(_lightingService, update.DeviceId, ze.ZoneId, ze.Effect!);
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
				EffectSerializer.DeserializeAndSet(_lightingService, update.DeviceId, ze.ZoneId, ze.Effect!);
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

	public async IAsyncEnumerable<DeviceBrightnessLevel> WatchBrightnessAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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
}
