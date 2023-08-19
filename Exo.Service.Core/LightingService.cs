using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Service;

public sealed class LightingService : IAsyncDisposable, ILightingServiceInternal
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

	private sealed class LightingZoneState
	{
		public ILightingZone? LightingZone;
		public required LightingEffect SerializedCurrentEffect;
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
	private readonly ConcurrentDictionary<(Guid, Guid), LightingZoneState> _lightingZones;
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
				ValueTask applyChangesTask = ValueTask.CompletedTask;

				lock (_lock)
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Addition:
						bool shouldApplyChanges = false;

						var lightingDriver = Unsafe.As<IDeviceDriver<ILightingDeviceFeature>>(notification.Driver!);

						LightingZoneInformation? unifiedLightingZone = null;
						if (lightingDriver.Features.GetFeature<IUnifiedLightingFeature>() is { } unifiedLightingFeature)
						{
							unifiedLightingZone = new LightingZoneInformation(unifiedLightingFeature.ZoneId, GetSupportedEffects(unifiedLightingFeature.GetType()).AsImmutable());
							if (_lightingZones.TryGetValue((notification.DeviceInformation.Id, unifiedLightingFeature.ZoneId), out var state))
							{
								// We restore the effect from the saved state if available.
								state.LightingZone = unifiedLightingFeature;
								EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, unifiedLightingFeature.ZoneId, state.SerializedCurrentEffect);
								shouldApplyChanges = true;
							}
							else
							{
								var effect = unifiedLightingFeature.GetCurrentEffect();
								state = new() { LightingZone = unifiedLightingFeature, SerializedCurrentEffect = EffectSerializer.Serialize(effect) };
								_lightingZones.TryAdd((notification.DeviceInformation.Id, unifiedLightingFeature.ZoneId), state);
							}
						}

						LightingZoneInformation[]? zones = null;
						if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lightingControllerFeature)
						{
							zones = new LightingZoneInformation[lightingControllerFeature.LightingZones.Count];

							int i = 0;
							foreach (var zone in lightingControllerFeature.LightingZones)
							{
								if (_lightingZones.TryGetValue((notification.DeviceInformation.Id, zone.ZoneId), out var state))
								{
									// We restore the effect from the saved state if available.
									state.LightingZone = zone;
									EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, zone.ZoneId, state.SerializedCurrentEffect);
									shouldApplyChanges = true;
								}
								else
								{
									var effect = zone.GetCurrentEffect();
									state = new() { LightingZone = zone, SerializedCurrentEffect = EffectSerializer.Serialize(effect) };
									_lightingZones.TryAdd((notification.DeviceInformation.Id, zone.ZoneId), state);
								}

								zones[i++] = new LightingZoneInformation(zone.ZoneId, GetSupportedEffects(zone.GetType()).AsImmutable());
							}
						}

						var lightingDeviceInformation = new LightingDeviceInformation(unifiedLightingZone, zones is not null ? zones.AsImmutable() : ImmutableArray<LightingZoneInformation>.Empty);
						var deviceDetails = new LightingDeviceDetails(notification.DeviceInformation, lightingDeviceInformation, notification.Driver!);

						_lightingDevices[notification.DeviceInformation.Id] = deviceDetails;

						if (shouldApplyChanges)
						{
							try
							{
								if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lcf)
								{
									applyChangesTask = lcf.ApplyChangesAsync();
								}
								else if (lightingDriver.Features.GetFeature<IUnifiedLightingFeature>() is { } ulf)
								{
									applyChangesTask = ulf.ApplyChangesAsync();
								}
							}
							catch
							{
								// TODO: Log
							}
						}

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
							if (unifiedLightingZone is not null && _lightingZones.TryGetValue((notification.DeviceInformation.Id, unifiedLightingZone.ZoneId), out var state))
							{
								_effectUpdated.Invoke(new(notification.DeviceInformation.Id, unifiedLightingZone.ZoneId, state.SerializedCurrentEffect));
							}
							if (zones is not null)
							{
								foreach (var zoneInformation in zones)
								{
									if (_lightingZones.TryGetValue((notification.DeviceInformation.Id, zoneInformation.ZoneId), out state))
									{
										_effectUpdated.Invoke(new(notification.DeviceInformation.Id, zoneInformation.ZoneId, state.SerializedCurrentEffect));
									}
								}
							}
						}
						break;
					case WatchNotificationKind.Removal:
						if (_lightingDevices.TryRemove(notification.DeviceInformation.Id, out var details))
						{
							if (details.LightingDeviceInformation.UnifiedLightingZone is not null)
							{
								if (_lightingZones.TryGetValue((notification.DeviceInformation.Id, details.LightingDeviceInformation.UnifiedLightingZone.ZoneId), out var state))
								{
									Volatile.Write(ref state.LightingZone, null);
								}
							}

							foreach (var zone in details.LightingDeviceInformation.LightingZones)
							{
								if (_lightingZones.TryGetValue((notification.DeviceInformation.Id, zone.ZoneId), out var state))
								{
									Volatile.Write(ref state.LightingZone, null);
								}
							}

							try
							{
								_devicesUpdated.Invoke(notification.Kind, details);
							}
							catch (AggregateException)
							{
								// TODO: Log
							}
						}
						break;
					}
				}

				try
				{
					await applyChangesTask.ConfigureAwait(false);
				}
				catch
				{
					// TODO: Log
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
				if (kvp.Value.LightingDeviceInformation.UnifiedLightingZone is not null && _lightingZones.TryGetValue((kvp.Key, kvp.Value.LightingDeviceInformation.UnifiedLightingZone.ZoneId), out var state))
				{
					initialNotifications.Add(new(kvp.Value.DeviceInformation.Id, kvp.Value.LightingDeviceInformation.UnifiedLightingZone.ZoneId, state.SerializedCurrentEffect));
				}
				foreach (var zoneInformation in kvp.Value.LightingDeviceInformation.LightingZones)
				{
					if (_lightingZones.TryGetValue((kvp.Key, zoneInformation.ZoneId), out state))
					{
						initialNotifications.Add(new(kvp.Value.DeviceInformation.Id, zoneInformation.ZoneId, state.SerializedCurrentEffect));
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

	// There are two entry points for SetEffect.
	// This one will set the effect based on a serialized version of it.
	public void SetEffect(Guid deviceId, Guid zoneId, LightingEffect effect)
		=> EffectSerializer.DeserializeAndSet(this, deviceId, zoneId, effect);

	// There are two entry points for SetEffect.
	// This one will set the effect based on a strongly typed value.
	public void SetEffect<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect)
		where TEffect : struct, ILightingEffect
		=> SetEffectInternal(deviceId, zoneId, effect, EffectSerializer.Serialize(effect), false);

	void ILightingServiceInternal.SetEffect<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect, LightingEffect serializedEffect, bool isRestore)
		=> SetEffectInternal(deviceId, zoneId, effect, serializedEffect, isRestore);

	private void SetEffectInternal<TEffect>(Guid deviceId, Guid zoneId, in TEffect effect, LightingEffect serializedEffect, bool isRestore)
		where TEffect : struct, ILightingEffect
	{
		if (!_lightingZones.TryGetValue((deviceId, zoneId), out var state) || state.LightingZone is not { } zone)
		{
			throw new InvalidOperationException($"Could not find the zone with ID {zoneId:B} on the specified device.");
		}

		SetEffect(zone, effect);

		state.SerializedCurrentEffect = serializedEffect;

		if (!isRestore)
		{
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
							onEffectUpdated.Invoke(new(deviceId, zoneId, serializedEffect));
						}
					}
					catch
					{
						// TODO: Log
					}
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
			var lightingDriver = (IDeviceDriver<ILightingDeviceFeature>)device.Driver;

			// TODO: Improve the situation on this. There should only be a single ApplyChangesAsync method.
			if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lcf)
			{
				await lcf.ApplyChangesAsync().ConfigureAwait(false);
			}
			else if (lightingDriver.Features.GetFeature<IUnifiedLightingFeature>() is { } ulf)
			{
				await ulf.ApplyChangesAsync().ConfigureAwait(false);
			}
		}
	}
}
