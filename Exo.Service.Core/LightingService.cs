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
	private RefReadonlyAction<LightingEffectWatchNotification>[]? _effectUpdated;

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
							_lightingZones.TryAdd((notification.DeviceInformation.DeviceId, unifiedLightingFeature.ZoneId), unifiedLightingFeature);
						}

						LightingZoneInformation[]? zones = null;
						if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lightingControllerFeature)
						{
							zones = new LightingZoneInformation[lightingControllerFeature.LightingZones.Count];

							int i = 0;
							foreach (var zone in lightingControllerFeature.LightingZones)
							{
								_lightingZones.TryAdd((notification.DeviceInformation.DeviceId, zone.ZoneId), zone);
								zones[i++] = new LightingZoneInformation(zone.ZoneId, GetSupportedEffects(zone.GetType()).AsImmutable());
							}
						}

						var lightingDeviceInformation = new LightingDeviceInformation(unifiedLightingZone, zones is not null ? zones.AsImmutable() : ImmutableArray<LightingZoneInformation>.Empty);
						var deviceDetails = new LightingDeviceDetails(notification.DeviceInformation, lightingDeviceInformation, notification.Driver!);

						_lightingDevices[notification.DeviceInformation.DeviceId] = deviceDetails;

						try
						{
							_devicesUpdated.Invoke(notification.Kind, deviceDetails);
						}
						catch (AggregateException)
						{
							// TODO: Log
						}

						// Handlers can only be added from within the lock, so we can conditionally emit the new effect notifications based on the needs. (They can be removed at anytime)
						if (Volatile.Read(ref _effectUpdated) is not null)
						{
							if (unifiedLightingZone is not null && _lightingZones.TryGetValue((notification.DeviceInformation.DeviceId, unifiedLightingZone.ZoneId), out var zone))
							{
								_effectUpdated.Invoke(new(notification.DeviceInformation.DeviceId, unifiedLightingZone.ZoneId, zone.GetCurrentEffect()));
							}
							if (zones is not null)
							{
								foreach (var zoneInformation in zones)
								{
									if (_lightingZones.TryGetValue((notification.DeviceInformation.DeviceId, zoneInformation.ZoneId), out zone))
									{
										_effectUpdated.Invoke(new(notification.DeviceInformation.DeviceId, zoneInformation.ZoneId, zone.GetCurrentEffect()));
									}
								}
							}
						}
						break;
					case WatchNotificationKind.Removal:
						if (_lightingDevices.TryRemove(notification.DeviceInformation.DeviceId, out deviceDetails))
						{
							if (_lightingDevices.TryRemove(notification.DeviceInformation.DeviceId, out var details))
							{
								if (details.LightingDeviceInformation.UnifiedLightingZone is not null)
								{
									_lightingZones.TryRemove((notification.DeviceInformation.DeviceId, details.LightingDeviceInformation.UnifiedLightingZone.ZoneId), out _);
								}
								foreach (var zone in details.LightingDeviceInformation.LightingZones)
								{
									_lightingZones.TryRemove((notification.DeviceInformation.DeviceId, zone.ZoneId), out _);
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
		ChannelReader<LightingEffectWatchNotification> reader;

		var channel = Channel.CreateUnbounded<LightingEffectWatchNotification>(WatchChannelOptions);
		reader = channel.Reader;
		var writer = channel.Writer;

		RefReadonlyAction<LightingEffectWatchNotification> onEffectUpdated = (in LightingEffectWatchNotification n) => { writer.TryWrite(n); };

		var initialNotifications = new List<LightingEffectWatchNotification>();

		lock (_lock)
		{
			foreach (var kvp in _lightingDevices)
			{
				if (kvp.Value.LightingDeviceInformation.UnifiedLightingZone is not null && _lightingZones.TryGetValue((kvp.Key, kvp.Value.LightingDeviceInformation.UnifiedLightingZone.ZoneId), out var zone))
				{
					initialNotifications.Add(new(kvp.Value.DeviceInformation.DeviceId, kvp.Value.LightingDeviceInformation.UnifiedLightingZone.ZoneId, zone.GetCurrentEffect()));
				}
				foreach (var zoneInformation in kvp.Value.LightingDeviceInformation.LightingZones)
				{
					if (_lightingZones.TryGetValue((kvp.Key, zoneInformation.ZoneId), out zone))
					{
						initialNotifications.Add(new(kvp.Value.DeviceInformation.DeviceId, zoneInformation.ZoneId, zone.GetCurrentEffect()));
					}
				}
			}

			ArrayExtensions.InterlockedAdd(ref _effectUpdated, onEffectUpdated);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var notification in reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _effectUpdated, onEffectUpdated);
		}
	}

	public void SetEffect<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect)
		where TEffect : struct, ILightingEffect
	{
		if (!_lightingZones.TryGetValue((deviceId, zoneId), out var zone))
		{
			throw new InvalidOperationException($"Could not find the zone with ID {zoneId:B} on the specified device.");
		}

		SetEffect(zone, effect);

		// We are really careful about the value of the delegate here, as sending a notification implies boxing.
		// As such, it is best if we can avoid it.
		// While we can't avoid an overhead when the settings UI is running, this shouldn't be too much of a hassle, as the Garbage Collector will still kick in pretty fast.
		if (Volatile.Read(ref _effectUpdated) is not null)
		{
			// We probably strictly need this lock for consistency with the WatchEffectsAsync setup.
			lock (_lock)
			{
				try
				{
					if (_effectUpdated is { } onEffectUpdated)
					{
						onEffectUpdated.Invoke(new(deviceId, zoneId, effect));
					}
				}
				catch
				{
					// TODO: Log
				}
			}
		}
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
