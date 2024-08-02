using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Contracts;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcLightingService : ILightingService
{
	private readonly LightingService _lightingService;
	private readonly LightingEffectMetadataService _lightingEffectMetadataService;
	private readonly ILogger<GrpcLightingService> _logger;

	public GrpcLightingService(ILogger<GrpcLightingService> logger, LightingService lightingService, LightingEffectMetadataService lightingEffectMetadataService)
	{
		_logger = logger;
		_lightingService = lightingService;
		_lightingEffectMetadataService = lightingEffectMetadataService;
	}

	// TODO: Refactor the lighting service and remove the raw device-related stuff.
	// The remove notifications are also kind of a duplicate with device notifications, so they could maybe be removed.
	// Maybe simply having a GetLightingDeviceCapabilities call would be enough. => Let's have a simpler watcher instead. Only push device capabilities.
	public async IAsyncEnumerable<Contracts.Ui.Settings.LightingDeviceInformation> WatchLightingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcLightingServiceDeviceWatchStart();
		try
		{
			await foreach (var lightingDevice in _lightingService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
			{
				LightingPaletteCapabilities? paletteCapabilities = null;

				yield return new()
				{
					DeviceId = lightingDevice.DeviceId,
					BrightnessCapabilities = lightingDevice.BrightnessCapabilities is { } brightnessCapabilities ?
						new() { MinimumBrightness = brightnessCapabilities.MinimumValue, MaximumBrightness = brightnessCapabilities.MaximumValue } :
						null,
					PaletteCapabilities = paletteCapabilities,
					UnifiedLightingZone = lightingDevice.UnifiedLightingZone?.ToGrpc(),
					LightingZones = ImmutableArray.CreateRange(lightingDevice.LightingZones, z => z.ToGrpc()),
				};
			}
		}
		finally
		{
			_logger.GrpcLightingServiceDeviceWatchStop();
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
		=> new(_lightingEffectMetadataService.GetEffectInformation(typeReference.TypeId));

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
