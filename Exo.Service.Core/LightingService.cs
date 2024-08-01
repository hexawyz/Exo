using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Programming.Annotations;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("Lighting")]
[TypeId(0x85F9E09E, 0xFD66, 0x4F0A, 0xA2, 0x82, 0x3E, 0x3B, 0xFD, 0xEB, 0x5B, 0xC2)]
public sealed partial class LightingService : IAsyncDisposable, ILightingServiceInternal
{
	private sealed class DeviceState
	{
		public Driver? Driver { get; set; }

		public Dictionary<Guid, LightingZoneState> LightingZones { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> LightingZonesConfigurationContainer { get; }
		public BrightnessCapabilities? BrightnessCapabilities { get; set; }

		public Guid UnifiedLightingZoneId { get; set; }

		public bool IsUnifiedLightingEnabled { get; set; }
		public byte? Brightness { get; set; }

		// Gets the object used to restrict concurrent accesses to the device.
		// (Yes, we'll lock on the object itself. Let's make of good use of those object header bytes here.)
		public object Lock => this;

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> lightingZonesConfigurationContainer,
			BrightnessCapabilities? brightnessCapabilities,
			Guid unifiedLightingZoneId,
			Dictionary<Guid, LightingZoneState> lightingZones
		)
		{
			DeviceConfigurationContainer = deviceConfigurationContainer;
			LightingZonesConfigurationContainer = lightingZonesConfigurationContainer;
			BrightnessCapabilities = brightnessCapabilities;
			LightingZones = lightingZones;
			UnifiedLightingZoneId = unifiedLightingZoneId;
		}
	}

	private sealed class LightingZoneState
	{
		public ILightingZone? LightingZone;
		public ImmutableArray<Guid> SupportedEffectTypeIds;
		public LightingEffect? SerializedCurrentEffect;
	}

	[TypeId(0x8EF5FD05, 0x960B, 0x449C, 0xA2, 0x01, 0xC6, 0x58, 0x99, 0x00, 0x20, 0x8E)]
	private readonly struct PersistedLightingDeviceInformation
	{
		public BrightnessCapabilities? BrightnessCapabilities { get; init; }
		public Guid? UnifiedLightingZoneId { get; init; }
	}

	[TypeId(0xB6677089, 0x77FE, 0x467A, 0x8C, 0x23, 0x87, 0x8C, 0x80, 0x71, 0x03, 0x19)]
	private readonly struct PersistedLightingZoneInformation
	{
		public PersistedLightingZoneInformation(LightingZoneInformation info)
		{
			SupportedEffectTypeIds = info.SupportedEffectTypeIds;
		}

		public ImmutableArray<Guid> SupportedEffectTypeIds { get; init; }
	}

	[TypeId(0x70F0F081, 0x39F1, 0x4C4C, 0xB5, 0x10, 0x03, 0x7B, 0xDB, 0x14, 0xCB, 0x72)]
	private readonly struct PersistedLightingDeviceConfiguration
	{
		public bool IsUnifiedLightingEnabled { get; init; }
		public byte? Brightness { get; init; }
	}

	private static readonly ConditionalWeakTable<Type, Tuple<Type[], Guid[]>> SupportedEffectCache = new();

	private static Tuple<Type[], Guid[]> GetSupportedEffects(Type lightingZoneType)
		=> SupportedEffectCache.GetValue(lightingZoneType, GetNonCachedSupportedEffects);

	private static Tuple<Type[], Guid[]> GetNonCachedSupportedEffects(Type lightingZoneType)
	{
		var supportedEffectList = new List<Type>();
		foreach (var interfaceType in lightingZoneType.GetInterfaces())
		{
			var t = interfaceType;
			while (t.BaseType is not null)
			{
				t = t.BaseType;
			}

			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILightingZoneEffect<>))
			{
				supportedEffectList.Add(t.GetGenericArguments()[0]);
			}
		}

		var supportedEffects = supportedEffectList.ToArray();
		return Tuple.Create(supportedEffects, Array.ConvertAll(supportedEffects, TypeId.Get));
	}

	private const string LightingConfigurationContainerName = "lit";

	public static async ValueTask<LightingService> CreateAsync
	(
		ILogger<LightingService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		CancellationToken cancellationToken
	)
	{
		// Make sure that the "not available" effect is in fact always available 😅
		// I probably want to get rid of this, as I think it would be better to just return and store null instead, but for now, this will do the trick.
		_ = EffectSerializer.GetEffectInformation(typeof(NotApplicableEffect));

		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			Guid? unifiedLightingZoneId = null;
			byte? brightness = null;
			BrightnessCapabilities? brightnessCapabilities = null;
			bool isUnifiedLightingEnabled = false;

			{
				var result = await deviceConfigurationContainer.ReadValueAsync<PersistedLightingDeviceInformation>(cancellationToken).ConfigureAwait(false);
				if (result.Found)
				{
					var info = result.Value;
					unifiedLightingZoneId = info.UnifiedLightingZoneId;
					brightnessCapabilities = info.BrightnessCapabilities;
				}
			}

			{
				var result = await deviceConfigurationContainer.ReadValueAsync<PersistedLightingDeviceConfiguration>(cancellationToken).ConfigureAwait(false);
				if (result.Found)
				{
					var config = result.Value;
					isUnifiedLightingEnabled = config.IsUnifiedLightingEnabled;
					brightness = config.Brightness;
				}
			}

			if (deviceConfigurationContainer.TryGetContainer(LightingConfigurationContainerName, GuidNameSerializer.Instance) is not { } lightingZoneConfigurationConfigurationContainer)
			{
				continue;
			}

			var lightingZoneIds = await lightingZoneConfigurationConfigurationContainer.GetKeysAsync(cancellationToken);

			if (lightingZoneIds.Length == 0)
			{
				continue;
			}

			var lightingZones = new Dictionary<Guid, LightingZoneState>();

			foreach (var lightingZoneId in lightingZoneIds)
			{
				var state = new LightingZoneState();
				{
					var result = await lightingZoneConfigurationConfigurationContainer.ReadValueAsync<PersistedLightingZoneInformation>(lightingZoneId, cancellationToken).ConfigureAwait(false);
					if (result.Found)
					{
						var info = result.Value;
						state.SupportedEffectTypeIds = info.SupportedEffectTypeIds;
					}
				}
				{
					var result = await lightingZoneConfigurationConfigurationContainer.ReadValueAsync<LightingEffect>(lightingZoneId, cancellationToken).ConfigureAwait(false);
					if (result.Found)
					{
						state.SerializedCurrentEffect = result.Value;
					}
				}
				if (!state.SupportedEffectTypeIds.IsDefaultOrEmpty && state.SerializedCurrentEffect is not null)
				{
					lightingZones.Add(lightingZoneId, state);
				}
			}

			if (lightingZones.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					new DeviceState
					(
						deviceConfigurationContainer,
						lightingZoneConfigurationConfigurationContainer,
						brightnessCapabilities,
						unifiedLightingZoneId.GetValueOrDefault(),
						lightingZones
					)
				);
			}
		}

		return new LightingService(logger, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ConcurrentDictionary<Guid, DeviceState> _lightingDeviceStates;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly object _changeLock;
	private ChannelWriter<LightingDeviceInformation>[]? _deviceListeners;
	private ChannelWriter<LightingEffectWatchNotification>[]? _effectChangeListeners;
	private ChannelWriter<LightingBrightnessWatchNotification>[]? _brightnessChangeListeners;
	private readonly ILogger<LightingService> _logger;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	private LightingService
	(
		ILogger<LightingService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ConcurrentDictionary<Guid, DeviceState> lightingDeviceStates
	)
	{
		_logger = logger;
		_deviceWatcher = deviceWatcher;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_lightingDeviceStates = lightingDeviceStates;
		_changeLock = new();
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
						await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceArrivalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				case WatchNotificationKind.Removal:
					try
					{
						OnDriverRemoved(notification);
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceRemovalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private static LightingDeviceInformation CreateDeviceInformation(Guid deviceId, DeviceState deviceState)
	{
		LightingZoneInformation? unifiedLightingZoneInfo = null;
		LightingZoneInformation[] lightingZoneInfos;

		if (deviceState.LightingZones.TryGetValue(deviceState.UnifiedLightingZoneId, out var unifiedLightingZoneState))
		{
			unifiedLightingZoneInfo = CreateLightingZoneInformation(deviceState.UnifiedLightingZoneId, unifiedLightingZoneState);
			lightingZoneInfos = new LightingZoneInformation[deviceState.LightingZones.Count - 1];
			int i = 0;
			foreach (var kvp in deviceState.LightingZones)
			{
				if (kvp.Key != deviceState.UnifiedLightingZoneId)
				{
					lightingZoneInfos[i++] = CreateLightingZoneInformation(kvp.Key, kvp.Value);
				}
			}
		}
		else
		{
			lightingZoneInfos = new LightingZoneInformation[deviceState.LightingZones.Count];
			int i = 0;
			foreach (var kvp in deviceState.LightingZones)
			{
				lightingZoneInfos[i++] = CreateLightingZoneInformation(kvp.Key, kvp.Value);
			}
		}

		return new
		(
			deviceId,
			deviceState.BrightnessCapabilities,
			unifiedLightingZoneInfo,
			ImmutableCollectionsMarshal.AsImmutableArray(lightingZoneInfos)
		);
	}

	private static LightingZoneInformation CreateLightingZoneInformation(Guid zoneId, LightingZoneState zoneState)
		=> new(zoneId, zoneState.SupportedEffectTypeIds);

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		Dictionary<Guid, LightingZoneState> lightingZoneStates;
		var applyChangesTask = ValueTask.CompletedTask;
		bool shouldApplyChanges = false;
		Guid unifiedLightingZoneId = default;
		Guid lightingZoneId;
		BrightnessCapabilities? brightnessCapabilities = null;
		Tuple<Type[], Guid[]>? supportedEffectsAndIds = null;
		byte? brightness = null;
		bool isUnifiedLightingEnabled = false;

		var lightingFeatures = (IDeviceFeatureSet<ILightingDeviceFeature>)notification.FeatureSet!;

		var lightingControllerFeature = lightingFeatures.GetFeature<ILightingControllerFeature>();
		var lightingZones = lightingControllerFeature?.LightingZones ?? Array.Empty<ILightingZone>();

		var unifiedLightingFeature = lightingFeatures.GetFeature<IUnifiedLightingFeature>();
		if (unifiedLightingFeature is not null)
		{
			unifiedLightingZoneId = unifiedLightingFeature.ZoneId;
			isUnifiedLightingEnabled = unifiedLightingFeature.IsUnifiedLightingEnabled;
		}

		// For now, ignore devices that have neither of the two main features.
		// To be seen if we'd want to have other features without these ones. (Doesn't seem to make sense, but let's see if the case presents itself)
		if (lightingControllerFeature is null && unifiedLightingFeature is null)
		{
			// TODO: Log a warning.
			return;
		}

		var brightnessFeature = lightingFeatures.GetFeature<ILightingBrightnessFeature>();
		if (brightnessFeature is not null)
		{
			brightnessCapabilities = new() { MinimumValue = brightnessFeature.MinimumBrightness, MaximumValue = brightnessFeature.MaximumBrightness };
			brightness = brightnessFeature.CurrentBrightness;
		}

		var changedLightingZones = new HashSet<Guid>();
		var effectLoader = new LightingEffectLoader();

		// If the arrived device is a new device, we can create a new state from scratch and retrieve the current configuration from the driver.
		// NB: Some drivers may hardcode the initial configuration if the device lacks the capability to read current settings.
		// Otherwise, we will need to detect possible changes since the last time the device was seen.
		// NB: Device is not supposed to change since the last time it was seen, but it can happen after a driver upgrade or maybe after some hardware update. (e.g. an extension was connected)
		if (!_lightingDeviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			var deviceContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
			var lightingZonesContainer = deviceContainer.GetContainer(LightingConfigurationContainerName, GuidNameSerializer.Instance);

			lightingZoneStates = new Dictionary<Guid, LightingZoneState>();

			if (unifiedLightingFeature is not null)
			{
				changedLightingZones.Add(unifiedLightingZoneId);
				supportedEffectsAndIds = GetSupportedEffects(unifiedLightingFeature.GetType());
				effectLoader.RegisterEffects(supportedEffectsAndIds.Item1);
				lightingZoneStates.Add
				(
					unifiedLightingZoneId,
					new()
					{
						SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2),
						LightingZone = unifiedLightingFeature,
						SerializedCurrentEffect = isUnifiedLightingEnabled ? EffectSerializer.Serialize(unifiedLightingFeature.GetCurrentEffect()) : null
					}
				);
			}

			foreach (var lightingZone in lightingZones)
			{
				lightingZoneId = lightingZone.ZoneId;
				if (!changedLightingZones.Add(lightingZoneId))
				{
					throw new InvalidOperationException($"Duplicate lighting zone ID: {lightingZoneId}.");
				}
				supportedEffectsAndIds = GetSupportedEffects(lightingZone.GetType());
				effectLoader.RegisterEffects(supportedEffectsAndIds.Item1);
				lightingZoneStates.Add
				(
					lightingZoneId,
					new()
					{
						SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2),
						LightingZone = lightingZone,
						SerializedCurrentEffect = isUnifiedLightingEnabled ? null : EffectSerializer.Serialize(lightingZone.GetCurrentEffect())
					}
				);
			}

			deviceState = new(deviceContainer, lightingZonesContainer, brightnessCapabilities, unifiedLightingZoneId, lightingZoneStates)
			{
				Driver = notification.Driver,
				IsUnifiedLightingEnabled = isUnifiedLightingEnabled,
				Brightness = brightness
			};

			foreach (var kvp in lightingZoneStates)
			{
				await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
				(
					kvp.Key,
					new PersistedLightingZoneInformation { SupportedEffectTypeIds = kvp.Value.SupportedEffectTypeIds },
					cancellationToken
				).ConfigureAwait(false);

				if (kvp.Value.SerializedCurrentEffect is { } effect)
				{
					await deviceState.LightingZonesConfigurationContainer.WriteValueAsync(kvp.Key, effect, cancellationToken).ConfigureAwait(false);
				}
			}

			lock (_changeLock)
			{
				_lightingDeviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
			}
		}
		else
		{
			lightingZoneStates = deviceState.LightingZones;

			var oldLightingZones = new HashSet<Guid>();
			if (deviceState.Brightness is not null && brightnessFeature is not null)
			{
				SetBrightness(notification.DeviceInformation.Id, deviceState.Brightness.GetValueOrDefault(), true);
				shouldApplyChanges = true;
			}
			else
			{
				// TODO: Should we consider preserving the brightness value if a device's driver loses the brightness feature at some point ?
				deviceState.Brightness = null;
			}

			// Add all the known lighting zones to the list of listing zones that potentially need to be removed.
			foreach (var key in deviceState.LightingZones.Keys)
			{
				oldLightingZones.Add(key);
			}

			// Take into account the unified lighting zone.
			if (unifiedLightingFeature is not null)
			{
				oldLightingZones.Remove(unifiedLightingZoneId);
				changedLightingZones.Add(unifiedLightingZoneId);
				// Make sure that the serialization for all effects is properly setup.
				effectLoader.RegisterEffects(GetSupportedEffects(unifiedLightingFeature.GetType()).Item1);
			}

			// Take into account the other lighting zones.
			foreach (var lightingZone in lightingZones)
			{
				lightingZoneId = lightingZone.ZoneId;
				oldLightingZones.Remove(lightingZoneId);
				if (!changedLightingZones.Add(lightingZoneId))
				{
					throw new InvalidOperationException($"Duplicate lighting zone ID: {lightingZoneId}.");
				}
				// Make sure that the serialization for all effects is properly setup.
				effectLoader.RegisterEffects(GetSupportedEffects(lightingZone.GetType()).Item1);
			}

			// After the previous steps, reste the HashSet and start listing the changed lighting zones instead.
			changedLightingZones.Clear();

			// After the steps above, we know for sure that there isn't any conflict with lighting zone IDs, and we can start killing old states.
			// First, we remove the configuration, outside the device state lock.
			foreach (var oldLightingZoneId in oldLightingZones)
			{
				await deviceState.LightingZonesConfigurationContainer.DeleteValuesAsync(oldLightingZoneId).ConfigureAwait(false);
			}

			lock (_changeLock)
			{
				lock (deviceState.Lock)
				{
					// Within the lock, remove old lighting zones.
					foreach (var oldLightingZoneId in oldLightingZones)
					{
						deviceState.LightingZones.Remove(oldLightingZoneId);
					}

					if (unifiedLightingFeature is not null)
					{
						deviceState.UnifiedLightingZoneId = unifiedLightingZoneId;
						if (lightingZoneStates.TryGetValue(unifiedLightingZoneId, out var lightingZoneState))
						{
							lightingZoneState.LightingZone = unifiedLightingFeature;
							UpdateSupportedEffects(unifiedLightingZoneId, lightingZoneState, unifiedLightingFeature.GetType(), changedLightingZones);
							// We restore the effect from the saved state if available.
							if (lightingZoneState.SerializedCurrentEffect is { } effect)
							{
								if (isUnifiedLightingEnabled)
								{
									EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, unifiedLightingZoneId, effect);
									shouldApplyChanges = true;
								}
							}
							else
							{
								lightingZoneState.SerializedCurrentEffect = EffectSerializer.Serialize(unifiedLightingFeature.GetCurrentEffect());
								changedLightingZones.Add(unifiedLightingZoneId);
							}
						}
						else
						{
							lightingZoneStates.Add
							(
								unifiedLightingZoneId,
								new()
								{
									SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(GetSupportedEffects(unifiedLightingFeature.GetType()).Item2),
									LightingZone = unifiedLightingFeature,
									SerializedCurrentEffect = EffectSerializer.Serialize(unifiedLightingFeature.GetCurrentEffect())
								}
							);
							changedLightingZones.Add(unifiedLightingZoneId);
						}
					}
					else
					{
						deviceState.UnifiedLightingZoneId = default;
					}

					foreach (var lightingZone in lightingZones)
					{
						lightingZoneId = lightingZone.ZoneId;

						if (lightingZoneStates.TryGetValue(lightingZoneId, out var lightingZoneState))
						{
							lightingZoneState.LightingZone = lightingZone;
							UpdateSupportedEffects(lightingZone.ZoneId, lightingZoneState, lightingZone.GetType(), changedLightingZones);
							// We restore the effect from the saved state if available.
							if (lightingZoneState.SerializedCurrentEffect is { } effect)
							{
								if (!isUnifiedLightingEnabled)
								{
									EffectSerializer.DeserializeAndRestore(this, notification.DeviceInformation.Id, lightingZoneId, effect);
									shouldApplyChanges = true;
								}
							}
							else
							{
								lightingZoneState.SerializedCurrentEffect = EffectSerializer.Serialize(lightingZone.GetCurrentEffect());
								changedLightingZones.Add(lightingZoneId);
							}
						}
						else
						{
							lightingZoneStates.Add
							(
								lightingZone.ZoneId,
								new()
								{
									SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(GetSupportedEffects(lightingZone.GetType()).Item2),
									LightingZone = lightingZone,
									SerializedCurrentEffect = EffectSerializer.Serialize(lightingZone.GetCurrentEffect())
								}
							);
							changedLightingZones.Add(lightingZoneId);
						}
					}

					deviceState.BrightnessCapabilities = brightnessCapabilities;
					deviceState.Driver = notification.Driver;
					deviceState.IsUnifiedLightingEnabled = isUnifiedLightingEnabled;
					deviceState.Brightness = brightness;

					if (shouldApplyChanges)
					{
						if (lightingFeatures.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
						{
							applyChangesTask = ApplyChangesAsync(dcf, notification.DeviceInformation.Id);
						}
					}
				}

				// Handlers can only be added from within the lock, so we can conditionally emit the new notifications based on the needs. (Handlers can be removed at anytime)
				if (Volatile.Read(ref _deviceListeners) is { } deviceListeners)
				{
					deviceListeners.TryWrite(CreateDeviceInformation(notification.DeviceInformation.Id, deviceState));
				}
				if (Volatile.Read(ref _brightnessChangeListeners) is { } brightnessChangeListeners && deviceState.Brightness is not null)
				{
					brightnessChangeListeners.TryWrite(new(notification.DeviceInformation.Id, deviceState.Brightness.GetValueOrDefault()));
				}
				if (Volatile.Read(ref _effectChangeListeners) is { } effectChangeListeners)
				{
					foreach (var kvp in deviceState.LightingZones)
					{
						effectChangeListeners.TryWrite(new(notification.DeviceInformation.Id, kvp.Key, kvp.Value.SerializedCurrentEffect));
					}
				}
			}

			foreach (var changedLightingZoneKey in changedLightingZones)
			{
				if (deviceState.LightingZones.TryGetValue(changedLightingZoneKey, out var changedLightingZone))
				{
					await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
					(
						changedLightingZoneKey,
						new PersistedLightingZoneInformation { SupportedEffectTypeIds = changedLightingZone.SupportedEffectTypeIds },
						cancellationToken
					).ConfigureAwait(false);

					if (changedLightingZone.SerializedCurrentEffect is { } effect)
					{
						await deviceState.LightingZonesConfigurationContainer.WriteValueAsync(changedLightingZoneKey, effect, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		await applyChangesTask.ConfigureAwait(false);
	}

	private static void UpdateSupportedEffects(Guid unifiedLightingZoneId, LightingZoneState lightingZoneState, Type lightingZoneType, HashSet<Guid> newLightingZones)
	{
		var supportedEffectsAndIds = GetSupportedEffects(lightingZoneType);
		// If the effects reference is exactly the same, we can skip everything and do nothing.
		if (ReferenceEquals(ImmutableCollectionsMarshal.AsArray(lightingZoneState.SupportedEffectTypeIds), supportedEffectsAndIds.Item2)) return;
		// Otherwise, if the list of supported effects has changed, the lighting zone must be updated.
		if (!lightingZoneState.SupportedEffectTypeIds.AsSpan().SequenceEqual(supportedEffectsAndIds.Item2))
		{
			// Mark that this lighting zone must be persisted again.
			newLightingZones.Add(unifiedLightingZoneId);
		}
		// Always update the effect reference. It the contents were equal, it will let us get rid of the old copy, and only keep the one that was retrieved from the cache.
		lightingZoneState.SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2);

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
			var n = CreateDeviceInformation(notification.DeviceInformation.Id, deviceState);

			lock (deviceState.Lock)
			{
				foreach (var lightingZoneState in deviceState.LightingZones.Values)
				{
					Volatile.Write(ref lightingZoneState.LightingZone, null);
				}
			}

			lock (_changeLock)
			{
				_deviceListeners.TryWrite(n);
			}
		}
	}

	public async IAsyncEnumerable<LightingDeviceInformation> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<LightingDeviceInformation>();

		var initialNotifications = new List<LightingDeviceInformation>();

		lock (_changeLock)
		{
			foreach (var kvp in _lightingDeviceStates)
			{
				initialNotifications.Add(CreateDeviceInformation(kvp.Key, kvp.Value));
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

		lock (_changeLock)
		{
			foreach (var kvp in _lightingDeviceStates)
			{
				foreach (var kvp2 in kvp.Value.LightingZones)
				{
					initialNotifications.Add(new(kvp.Key, kvp2.Key, kvp2.Value.SerializedCurrentEffect));
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

		lock (_changeLock)
		{
			foreach (var kvp in _lightingDeviceStates)
			{
				if (kvp.Value.Brightness is byte brightness)
				{
					initialNotifications.Add(new(kvp.Key, brightness));
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
					lock (_changeLock)
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

				var lightingFeatures = deviceState.Driver.GetFeatureSet<ILightingDeviceFeature>();

				if (lightingFeatures.GetFeature<ILightingBrightnessFeature>() is { } bf)
				{
					bf.CurrentBrightness = brightness;
				}
			}

			if (!isRestore)
			{
				if (Volatile.Read(ref _brightnessChangeListeners) is not null)
				{
					// We probably strictly need this lock for consistency with the WatchBrightnessAsync setup.
					lock (_changeLock)
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

				var lightingFeatures = deviceState.Driver.GetFeatureSet<ILightingDeviceFeature>();

				if (lightingFeatures.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
				{
					applyChangesTask = dcf.ApplyChangesAsync();
				}
			}
		}

	Completed:;
		return applyChangesTask;
	}
}
