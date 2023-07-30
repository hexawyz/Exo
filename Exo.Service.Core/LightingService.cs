using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Service;

public class LightingService : IAsyncDisposable
{
	private sealed class LightingDeviceDetails
	{
		public LightingDeviceDetails(DeviceInformation deviceInformation, LightingDeviceInformation lightingDeviceInformation, Driver driver)
		{
			DeviceInformation = deviceInformation;
			Driver = driver;
			LightingDeviceInformation = lightingDeviceInformation;
		}

		public DeviceInformation DeviceInformation { get; }
		public LightingDeviceInformation LightingDeviceInformation { get; }
		public Driver Driver { get; }
	}

	private static readonly ConditionalWeakTable<Type, Type[]> SupportedEffectCache = new();

	private static Type[] GetSupportedEffects(Type lightingZoneType)
		=> SupportedEffectCache.GetValue(lightingZoneType, GetNonCachedSupportedEffects);

	private static Type[] GetNonCachedSupportedEffects(Type lightingZoneType)
	{
		var supportedEffects = new List<Type>();
		foreach (var interfaceType in lightingZoneType.GetInterfaces())
		{
			var t = interfaceType;
			while (t.BaseType is not null)
			{
				t = t.BaseType;
			}

			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILightingZoneEffect<>))
			{
				supportedEffects.Add(t.GetGenericArguments()[0]);
			}
		}

		return supportedEffects.ToArray();
	}

	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ConcurrentDictionary<Guid, LightingDeviceDetails> _lightingDevices;
	private readonly ConcurrentDictionary<(Guid, Guid), ILightingZone> _lightingZones;
	private readonly object _lock; // Only needed in order to output a reliable stream out of the service… 
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;
	private Action<WatchNotificationKind, LightingDeviceDetails>[]? _devicesUpdated;

	public LightingService(DriverRegistry driverRegistry)
	{
		_deviceWatcher = driverRegistry;
		_lightingDevices = new();
		_lightingZones = new();
		_lock = new();
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAsync<ILightingDeviceFeature>(cancellationToken))
			{
				lock (_lock)
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Addition:
						var lightingDriver = Unsafe.As<IDeviceDriver<ILightingDeviceFeature>>(notification.Driver!);

						LightingZoneInformation? unifiedLightingZone = null;
						if (lightingDriver.Features.GetFeature<IUnifiedLightingFeature>() is { } unifiedLightingFeature)
						{
							unifiedLightingZone = new LightingZoneInformation(unifiedLightingFeature.ZoneId, GetSupportedEffects(unifiedLightingFeature.GetType()).AsImmutable());
							_lightingZones.TryAdd((notification.DeviceInformation.UniqueId, unifiedLightingFeature.ZoneId), unifiedLightingFeature);
						}

						LightingZoneInformation[]? zones = null;
						if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lightingControllerFeature)
						{
							zones = new LightingZoneInformation[lightingControllerFeature.LightingZones.Count];

							int i = 0;
							foreach (var zone in lightingControllerFeature.LightingZones)
							{
								_lightingZones.TryAdd((notification.DeviceInformation.UniqueId, zone.ZoneId), zone);
								zones[i++] = new LightingZoneInformation(zone.ZoneId, GetSupportedEffects(zone.GetType()).AsImmutable());
							}
						}

						var lightingDeviceInformation = new LightingDeviceInformation(unifiedLightingZone, zones is not null ? zones.AsImmutable() : ImmutableArray<LightingZoneInformation>.Empty);
						var deviceDetails = new LightingDeviceDetails(notification.DeviceInformation, lightingDeviceInformation, notification.Driver!);

						_lightingDevices[notification.DeviceInformation.UniqueId] = deviceDetails;

						try
						{
							_devicesUpdated.Invoke(notification.Kind, deviceDetails);
						}
						catch (AggregateException)
						{
							// TODO: Log
						}
						break;
					case WatchNotificationKind.Removal:
						if (_lightingDevices.TryRemove(notification.DeviceInformation.UniqueId, out deviceDetails))
						{
							if (_lightingDevices.TryRemove(notification.DeviceInformation.UniqueId, out var details))
							{
								if (details.LightingDeviceInformation.UnifiedLightingZone is not null)
								{
									_lightingZones.TryRemove((notification.DeviceInformation.UniqueId, details.LightingDeviceInformation.UnifiedLightingZone.GetValueOrDefault().ZoneId), out _);
								}
								foreach (var zone in details.LightingDeviceInformation.LightingZones)
								{
									_lightingZones.TryRemove((notification.DeviceInformation.UniqueId, zone.ZoneId), out _);
								}
								try
								{
									_devicesUpdated.Invoke(notification.Kind, deviceDetails);
								}
								catch (AggregateException)
								{
									// TODO: Log
								}
							}
						}
						break;
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
	}

	private static readonly UnboundedChannelOptions WatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };

	public async IAsyncEnumerable<LightingDeviceWatchNotification> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ChannelReader<(bool IsAdded, DeviceInformation deviceInformation, LightingDeviceInformation lightingDeviceInformation, Driver? Driver)> reader;

		var channel = Channel.CreateUnbounded<(bool IsAdded, DeviceInformation deviceInformation, LightingDeviceInformation lightingDeviceInformation, Driver? Driver)>(WatchChannelOptions);
		reader = channel.Reader;
		var writer = channel.Writer;

		var onDriverUpdated = (WatchNotificationKind k, LightingDeviceDetails d) => { writer.TryWrite((k != WatchNotificationKind.Removal, d.DeviceInformation, d.LightingDeviceInformation, d.Driver)); };

		var initialNotifications = new List<LightingDeviceWatchNotification>();

		lock (_lock)
		{
			foreach (var kvp in _lightingDevices)
			{
				initialNotifications.Add(new(WatchNotificationKind.Enumeration, kvp.Value.DeviceInformation, kvp.Value.LightingDeviceInformation, kvp.Value.Driver));
			}

			ArrayExtensions.InterlockedAdd(ref _devicesUpdated, onDriverUpdated);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var (isAdded, deviceInformation, lightingDeviceInformation, driver) in reader.ReadAllAsync(cancellationToken))
			{
				yield return new(isAdded ? WatchNotificationKind.Addition : WatchNotificationKind.Removal, deviceInformation, lightingDeviceInformation, driver);
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _devicesUpdated, onDriverUpdated);
		}
	}

	public async IAsyncEnumerable<LightingEffectWatchNotification> WatchEffectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		yield break;
	}

	public void SetEffect<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect)
		where TEffect : struct, ILightingEffect
	{
		if (!_lightingZones.TryGetValue((deviceId, zoneId), out var zone))
		{
			throw new InvalidOperationException($"Could not find the zone with ID {zoneId:B} on the specified device.");
		}

		SetEffect(zone, effect);
	}

	private void SetEffect<TEffect>(ILightingZone lightingZone, in TEffect effect)
		where TEffect : struct, ILightingEffect
	{
		if (lightingZone is not ILightingZoneEffect<TEffect> zone)
		{
			throw new InvalidOperationException($"The specified zone does not support effects of type {effect.GetType()}.");
		}

		zone.ApplyEffect(effect);
	}

	public async ValueTask ApplyChanges(Guid deviceId)
	{
		if (_lightingDevices.TryGetValue(deviceId, out var device))
		{
			// TODO: Improve the situation on this. There should only be a single ApplyChangesAsync method.
			if (device.Driver is ILightingControllerFeature controllerFeature)
			{
				await controllerFeature.ApplyChangesAsync().ConfigureAwait(false);
			}
			else if (device.Driver is IUnifiedLightingFeature unifiedLightingFeature)
			{
				await unifiedLightingFeature.ApplyChangesAsync().ConfigureAwait(false);
			}
		}
	}
}
