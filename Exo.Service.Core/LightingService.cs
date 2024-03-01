using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Programming.Annotations;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("Lighting")]
[TypeId(0x85F9E09E, 0xFD66, 0x4F0A, 0xA2, 0x82, 0x3E, 0x3B, 0xFD, 0xEB, 0x5B, 0xC2)]
public sealed class LightingService : IAsyncDisposable, ILightingServiceInternal
{
	private sealed class DeviceState
	{
		private LatestDeviceDetails? _deviceDetails;
		private Driver? _driver;

		public DeviceState()
		{
			LightingZones = new();
		}

		// Should actually never be null after initialization, but that is difficult to enforce here.
		public LatestDeviceDetails? DeviceDetails
		{
			get => Volatile.Read(ref _deviceDetails);
			set => Volatile.Write(ref _deviceDetails, value);
		}

		public Driver? Driver
		{
			get => Volatile.Read(ref _driver);
			set => Volatile.Write(ref _driver, value);
		}

		// Gets the object used to restrict concurrent accesses to the device.
		// (Yes, we'll lock on the object itself. Let's make of good use of those object header bytes here.)
		public object Lock => this;

		// The state for the brightness will be preserved here.
		public byte? Brightness { get; set; }

		// The state for each lighting zone will be preserved too.
		public ConcurrentDictionary<Guid, LightingZoneState> LightingZones { get; }
	}

	// This information is put in a separate class because it needs to be updated atomically.
	// Device details will *generally* not change, but if they change for any reason, we should avoid any tearing on the data structures.
	// For this reason, we need to have *correct* equality comparers on all the contained types.
	// Comparing two objects will have a cost, but keeping long-lived objects untouched should be better for the GC.
	// The idea is that this data can, and *will* be persisted in configuration.
	// For later features, we need to be able to reference devices that are *not* online, and possibly schedule effects on them.
	// TODO: Persistance of DeviceInformation should be moved to driver registry.
	private sealed class LatestDeviceDetails : IEquatable<LatestDeviceDetails?>
	{
		public LatestDeviceDetails(DeviceStateInformation deviceInformation, LightingDeviceInformation lightingDeviceInformation)
		{
			DeviceInformation = deviceInformation;
			LightingDeviceInformation = lightingDeviceInformation;
		}

		public DeviceStateInformation DeviceInformation { get; }
		public LightingDeviceInformation LightingDeviceInformation { get; }

		public override bool Equals(object? obj) => Equals(obj as LatestDeviceDetails);

		public bool Equals(LatestDeviceDetails? other)
			=> other is not null &&
				EqualityComparer<DeviceStateInformation>.Default.Equals(DeviceInformation, other.DeviceInformation) &&
				LightingDeviceInformation.Equals(other.LightingDeviceInformation);

		public override int GetHashCode() => HashCode.Combine(DeviceInformation, LightingDeviceInformation);

		public static bool operator ==(LatestDeviceDetails? left, LatestDeviceDetails? right) => EqualityComparer<LatestDeviceDetails>.Default.Equals(left, right);
		public static bool operator !=(LatestDeviceDetails? left, LatestDeviceDetails? right) => !(left == right);
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
	private readonly ConcurrentDictionary<Guid, DeviceState> _lightingDeviceStates;
	private readonly object _lock; // Only needed in order to output a reliable stream out of the service… 
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;
	private ChannelWriter<LightingDeviceWatchNotification>[]? _deviceListeners;
	private ChannelWriter<LightingEffectWatchNotification>[]? _effectChangeListeners;
	private ChannelWriter<LightingBrightnessWatchNotification>[]? _brightnessChangeListeners;
	private readonly ILogger<LightingService> _logger;

	public LightingService(DeviceRegistry driverRegistry, ILogger<LightingService> logger)
	{
		_deviceWatcher = driverRegistry;
		_logger = logger;
		_lightingDeviceStates = new();
		_lock = new();
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ILightingDeviceFeature>(cancellationToken))
			{
				switch (notification.Kind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					try
					{
						await OnDriverAddedAsync(notification).ConfigureAwait(false);
					}
					catch
					{
					}
					break;
				case WatchNotificationKind.Removal:
					try
					{
						OnDriverRemoved(notification);
					}
					catch
					{
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private static LightingDeviceWatchNotification CreateNotification(WatchNotificationKind kind, DeviceState deviceState)
		=> new(kind, deviceState.DeviceDetails!.DeviceInformation, deviceState.DeviceDetails.LightingDeviceInformation, deviceState.Driver!);

	private ValueTask OnDriverAddedAsync(DeviceWatchNotification notification)
	{
		var applyChangesTask = ValueTask.CompletedTask;
		bool shouldApplyChanges = false;
		bool isNewState = false;
		LightingZoneInformation? unifiedLightingZone = null;
		LightingZoneInformation[]? zones = null;

		if (!_lightingDeviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			deviceState = new();
			isNewState = true;
		}

		var lightingZoneStates = deviceState.LightingZones;

		var lightingDriver = Unsafe.As<IDeviceDriver<ILightingDeviceFeature>>(notification.Driver!);

		lock (_lock)
		{
			// TODO: Refactoring to properly handle drivers with changing feature sets.
			// At the minimum, we should consider the device offline from the lighting service POV, but maybe the feature change notification is not good enough.
			// The better design might be a IDeviceFeature that lists the supported feature sets, the active ones, and provide more detailed notifications for when a feature set is enabled or not.
			// That's because we probably shouldn't expect a random feature to pop out of thin air among similar ones, but instead have something more like while feature categories being optionally enabled.
			if (lightingDriver.Features.IsEmpty) return ValueTask.CompletedTask;

			lock (deviceState.Lock)
			{
				if (lightingDriver.Features.GetFeature<ILightingBrightnessFeature>() is { } lightingBrightnessFeature)
				{
					if (!isNewState && deviceState.Brightness is not null)
					{
						SetBrightness(notification.DeviceInformation.Id, deviceState.Brightness.GetValueOrDefault(), true);
						shouldApplyChanges = true;
					}
					else
					{
						deviceState.Brightness = lightingBrightnessFeature.CurrentBrightness;
					}
				}
				else
				{
					// TODO: Should we consider preserving the brightness value if a device's driver loses the brightness feature at some point ?
					deviceState.Brightness = null;
				}

				if (lightingDriver.Features.GetFeature<IUnifiedLightingFeature>() is { } unifiedLightingFeature)
				{
					unifiedLightingZone = new LightingZoneInformation(unifiedLightingFeature.ZoneId, GetSupportedEffects(unifiedLightingFeature.GetType()).AsImmutable());
					if (lightingZoneStates.TryGetValue(unifiedLightingFeature.ZoneId, out var lightingZoneState))
					{
						// We restore the effect from the saved state if available.
						lightingZoneState.LightingZone = unifiedLightingFeature;
						EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, unifiedLightingFeature.ZoneId, lightingZoneState.SerializedCurrentEffect);
						shouldApplyChanges = true;
					}
					else
					{
						var effect = unifiedLightingFeature.GetCurrentEffect();
						lightingZoneState = new() { LightingZone = unifiedLightingFeature, SerializedCurrentEffect = EffectSerializer.Serialize(effect) };
						lightingZoneStates.TryAdd(unifiedLightingFeature.ZoneId, lightingZoneState);
					}
				}

				if (lightingDriver.Features.GetFeature<ILightingControllerFeature>() is { } lightingControllerFeature)
				{
					zones = new LightingZoneInformation[lightingControllerFeature.LightingZones.Count];

					int i = 0;
					foreach (var zone in lightingControllerFeature.LightingZones)
					{
						if (lightingZoneStates.TryGetValue(zone.ZoneId, out var lightingZoneState))
						{
							// We restore the effect from the saved state if available.
							lightingZoneState.LightingZone = zone;
							EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, zone.ZoneId, lightingZoneState.SerializedCurrentEffect);
							shouldApplyChanges = true;
						}
						else
						{
							var effect = zone.GetCurrentEffect();
							lightingZoneState = new() { LightingZone = zone, SerializedCurrentEffect = EffectSerializer.Serialize(effect) };
							lightingZoneStates.TryAdd(zone.ZoneId, lightingZoneState);
						}

						zones[i++] = new LightingZoneInformation(zone.ZoneId, GetSupportedEffects(zone.GetType()).AsImmutable());
					}
				}

				if (shouldApplyChanges)
				{
					if (lightingDriver.Features.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
					{
						applyChangesTask = ApplyChangesAsync(dcf, notification.DeviceInformation.Id);
					}
				}

				var details = new LatestDeviceDetails
				(
					notification.DeviceInformation,
					new
					(
						unifiedLightingZone,
						zones is not null ? zones.AsImmutable() : ImmutableArray<LightingZoneInformation>.Empty,
						deviceState.Brightness is not null
					)
				);

				if (details != deviceState.DeviceDetails)
				{
					deviceState.DeviceDetails = details;
				}
				deviceState.Driver = notification.Driver;

				if (isNewState)
				{
					_lightingDeviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
				}

				_deviceListeners.TryWrite(CreateNotification(notification.Kind, deviceState));
			}

			// Handlers can only be added from within the lock, so we can conditionally emit the new effect notifications based on the needs. (Handlers can be removed at anytime)
			if (Volatile.Read(ref _brightnessChangeListeners) is not null && deviceState.Brightness is not null)
			{
				_brightnessChangeListeners.TryWrite(new(notification.DeviceInformation.Id, deviceState.Brightness.GetValueOrDefault()));
			}
			if (Volatile.Read(ref _effectChangeListeners) is not null)
			{
				if (unifiedLightingZone is not null && lightingZoneStates.TryGetValue(unifiedLightingZone.ZoneId, out var state))
				{
					_effectChangeListeners.TryWrite(new(notification.DeviceInformation.Id, unifiedLightingZone.ZoneId, state.SerializedCurrentEffect));
				}
				if (zones is not null)
				{
					foreach (var zoneInformation in zones)
					{
						if (lightingZoneStates.TryGetValue(zoneInformation.ZoneId, out state))
						{
							_effectChangeListeners.TryWrite(new(notification.DeviceInformation.Id, zoneInformation.ZoneId, state.SerializedCurrentEffect));
						}
					}
				}
			}
		}

		return applyChangesTask;
	}

	private async ValueTask ApplyChangesAsync(ILightingDeferredChangesFeature deferredChangesFeature, Guid deviceId)
	{
		try
		{
			await deferredChangesFeature.ApplyChangesAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LightingServiceRestoreStateApplyChangesError(deviceId, ex);
		}
	}

	private void OnDriverRemoved(DeviceWatchNotification notification)
	{
		if (_lightingDeviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			var lightingZoneStates = deviceState.LightingZones;
			var lightingDeviceInformation = deviceState.DeviceDetails!.LightingDeviceInformation;

			lock (_lock)
			{
				var n = CreateNotification(notification.Kind, deviceState);

				lock (deviceState.Lock)
				{
					if (lightingDeviceInformation.UnifiedLightingZone is not null)
					{
						if (lightingZoneStates.TryGetValue(lightingDeviceInformation.UnifiedLightingZone.ZoneId, out var state))
						{
							Volatile.Write(ref state.LightingZone, null);
						}
					}

					foreach (var zone in lightingDeviceInformation.LightingZones)
					{
						if (lightingZoneStates.TryGetValue(zone.ZoneId, out var state))
						{
							Volatile.Write(ref state.LightingZone, null);
						}
					}
				}

				_deviceListeners.TryWrite(n);
			}
		}
	}

	public async IAsyncEnumerable<LightingDeviceWatchNotification> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<LightingDeviceWatchNotification>();

		var initialNotifications = new List<LightingDeviceWatchNotification>();

		lock (_lock)
		{
			foreach (var deviceState in _lightingDeviceStates.Values)
			{
				initialNotifications.Add(CreateNotification(WatchNotificationKind.Enumeration, deviceState));
			}

			ArrayExtensions.InterlockedAdd(ref _deviceListeners, channel);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceListeners, channel);
		}
	}

	public async IAsyncEnumerable<LightingEffectWatchNotification> WatchEffectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<LightingEffectWatchNotification>();

		var initialNotifications = new List<LightingEffectWatchNotification>();

		lock (_lock)
		{
			foreach (var deviceState in _lightingDeviceStates.Values)
			{
				var details = deviceState.DeviceDetails!;
				var lightingZoneStates = deviceState.LightingZones;

				if (details.LightingDeviceInformation.UnifiedLightingZone is not null && lightingZoneStates.TryGetValue(details.LightingDeviceInformation.UnifiedLightingZone.ZoneId, out var zoneState))
				{
					initialNotifications.Add(new(details.DeviceInformation.Id, details.LightingDeviceInformation.UnifiedLightingZone.ZoneId, zoneState.SerializedCurrentEffect));
				}
				foreach (var zoneInformation in details.LightingDeviceInformation.LightingZones)
				{
					if (lightingZoneStates.TryGetValue(zoneInformation.ZoneId, out zoneState))
					{
						initialNotifications.Add(new(details.DeviceInformation.Id, zoneInformation.ZoneId, zoneState.SerializedCurrentEffect));
					}
				}
			}

			ArrayExtensions.InterlockedAdd(ref _effectChangeListeners, channel);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _effectChangeListeners, channel);
		}
	}

	public async IAsyncEnumerable<LightingBrightnessWatchNotification> WatchBrightnessAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<LightingBrightnessWatchNotification>();
		var reader = channel.Reader;
		var writer = channel.Writer;

		var initialNotifications = new List<LightingBrightnessWatchNotification>();

		lock (_lock)
		{
			foreach (var deviceState in _lightingDeviceStates.Values)
			{
				if (deviceState.Brightness is byte brightness)
				{
					initialNotifications.Add(new(deviceState.DeviceDetails!.DeviceInformation.Id, brightness));
				}
			}

			ArrayExtensions.InterlockedAdd(ref _brightnessChangeListeners, writer);
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
			ArrayExtensions.InterlockedRemove(ref _brightnessChangeListeners, writer);
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
		if (!_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			throw new InvalidOperationException($"Could not find the specified device.");
		}

		lock (deviceState.Lock)
		{
			if (!deviceState.LightingZones.TryGetValue(zoneId, out var zoneState))
			{
				throw new InvalidOperationException($"Could not find the zone with ID {zoneId:B} on the specified device.");
			}

			if (zoneState.LightingZone is { } zone)
			{
				SetEffect(zone, effect);
			}

			zoneState.SerializedCurrentEffect = serializedEffect;

			if (!isRestore)
			{
				// We are really careful about the value of the delegate here, as sending a notification implies boxing.
				// As such, it is best if we can avoid it.
				// While we can't avoid an overhead when the settings UI is running, this shouldn't be too much of a hassle, as the Garbage Collector will still kick in pretty fast.
				if (Volatile.Read(ref _effectChangeListeners) is not null)
				{
					// We probably strictly need this lock for consistency with the WatchEffectsAsync setup.
					lock (_lock)
					{
						_effectChangeListeners.TryWrite(new(deviceId, zoneId, serializedEffect));
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

	public void SetBrightness(Guid deviceId, byte brightness) => SetBrightness(deviceId, brightness, false);

	private void SetBrightness(Guid deviceId, byte brightness, bool isRestore)
	{
		if (_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			lock (deviceState.Lock)
			{
				if (deviceState.Driver is null) return;

				var lightingDriver = (IDeviceDriver<ILightingDeviceFeature>)deviceState.Driver;

				if (lightingDriver.Features.GetFeature<ILightingBrightnessFeature>() is { } bf)
				{
					bf.CurrentBrightness = brightness;
				}
			}

			if (!isRestore)
			{
				if (Volatile.Read(ref _brightnessChangeListeners) is not null)
				{
					// We probably strictly need this lock for consistency with the WatchBrightnessAsync setup.
					lock (_lock)
					{
						_brightnessChangeListeners.TryWrite(new(deviceId, brightness));
					}
				}
			}
		}
	}

	public ValueTask ApplyChanges(Guid deviceId)
	{
		ValueTask applyChangesTask = ValueTask.CompletedTask;

		if (_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			lock (deviceState.Lock)
			{
				if (deviceState.Driver is null) goto Completed;

				var lightingDriver = (IDeviceDriver<ILightingDeviceFeature>)deviceState.Driver;

				if (lightingDriver.Features.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
				{
					applyChangesTask = dcf.ApplyChangesAsync();
				}
			}
		}

	Completed:;
		return applyChangesTask;
	}
}
