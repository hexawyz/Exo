using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.PowerManagement;
using Exo.Primitives;
using Exo.Programming.Annotations;
using Exo.Service.Configuration;
using Exo.Services;
using Microsoft.Extensions.Logging;

// I ideally would not rely on an external library to do the color conversions, but we already depend on ImageSharp for image stuff and that is unlikely to change. Might as well use it.
using ColorSpaceConverter = SixLabors.ImageSharp.ColorSpaces.Conversion.ColorSpaceConverter;
using ImageSharpRgb = SixLabors.ImageSharp.ColorSpaces.Rgb;
using RgbWorkingSpaces = SixLabors.ImageSharp.ColorSpaces.RgbWorkingSpaces;

namespace Exo.Service;

[Module("Lighting")]
[TypeId(0x85F9E09E, 0xFD66, 0x4F0A, 0xA2, 0x82, 0x3E, 0x3B, 0xFD, 0xEB, 0x5B, 0xC2)]
internal sealed partial class LightingService :
	IAsyncDisposable,
	IPowerNotificationSink,
	IChangeSource<LightingDeviceInformation>,
	IChangeSource<LightingDeviceConfiguration>,
	IChangeSource<LightingConfiguration>,
	IChangeSource<DisconnectedLightingDeviceInformation>
{
	private sealed class DeviceState
	{
		public Driver? Driver { get; set; }

		public Dictionary<Guid, LightingZoneState> LightingZones { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> LightingZonesConfigurationContainer { get; }
		public BrightnessCapabilities BrightnessCapabilities { get; set; }
		public LightingPersistenceMode PersistenceMode { get; set; }
		public LightingCapabilities Capabilities { get; set; }

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
			BrightnessCapabilities brightnessCapabilities,
			LightingPersistenceMode persistenceMode,
			LightingCapabilities capabilities,
			Guid unifiedLightingZoneId,
			Dictionary<Guid, LightingZoneState> lightingZones
		)
		{
			DeviceConfigurationContainer = deviceConfigurationContainer;
			LightingZonesConfigurationContainer = lightingZonesConfigurationContainer;
			BrightnessCapabilities = brightnessCapabilities;
			PersistenceMode = persistenceMode;
			Capabilities = capabilities;
			LightingZones = lightingZones;
			UnifiedLightingZoneId = unifiedLightingZoneId;
		}

		public PersistedLightingDeviceConfiguration CreatePersistedConfiguration()
			=> new(IsUnifiedLightingEnabled, Brightness);

		public LightingDeviceConfiguration CreateConfiguration(Guid deviceId)
			=> new(deviceId, IsUnifiedLightingEnabled, Brightness, [], CreateEffectConfiguration());

		private ImmutableArray<LightingZoneEffect> CreateEffectConfiguration()
		{
			var lightingZones = new LightingZoneEffect[LightingZones.Count];
			int i = 0;
			foreach (var kvp in LightingZones)
			{
				if (kvp.Value.SerializedCurrentEffect is not null)
				{
					lightingZones[i++] = new(kvp.Key, kvp.Value.SerializedCurrentEffect);
				}
			}
			if (i != lightingZones.Length) lightingZones = lightingZones[..i];
			return ImmutableCollectionsMarshal.AsImmutableArray(lightingZones);
		}
	}

	private sealed class LightingZoneState
	{
		public ILightingZone? LightingZone;
		public ImmutableArray<Guid> SupportedEffectTypeIds;
		public LightingEffect? SerializedCurrentEffect;
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
		IConfigurationContainer lightingConfigurationContainer,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		DeviceRegistry deviceWatcher,
		IPowerNotificationService powerNotificationService,
		CancellationToken cancellationToken
	)
	{
		bool useCentralizedLighting = false;
		LightingEffect[] centralizedLightingEffects;
		{
			var result = await lightingConfigurationContainer.ReadValueAsync(SourceGenerationContext.Default.PersistedLightingConfiguration, cancellationToken).ConfigureAwait(false);
			if (result.Found)
			{
				useCentralizedLighting = result.Value.IsCentralizedLightingEnabled;
				try
				{
					centralizedLightingEffects = BuildEffectFallbacks(result.Value.CentralizedLightingEffect);
					goto LightingEffectsAreValid;
				}
				catch
				{
					// TODO: Log
				}
			}
			centralizedLightingEffects = [new(DisabledEffectId, [])];
		LightingEffectsAreValid:;
		}

		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			Guid? unifiedLightingZoneId = null;
			byte? brightness = null;
			BrightnessCapabilities brightnessCapabilities = default;
			bool isUnifiedLightingEnabled = false;
			LightingPersistenceMode persistenceMode = LightingPersistenceMode.NeverPersisted;
			LightingCapabilities capabilities = LightingCapabilities.None;

			{
				var result = await deviceConfigurationContainer.ReadValueAsync(SourceGenerationContext.Default.PersistedLightingDeviceInformation, cancellationToken).ConfigureAwait(false);
				if (result.Found)
				{
					var info = result.Value;
					unifiedLightingZoneId = info.UnifiedLightingZoneId;
					brightnessCapabilities = info.BrightnessCapabilities.GetValueOrDefault();
					persistenceMode = info.PersistenceMode;
					capabilities = info.BrightnessCapabilities is not null ? info.Capabilities | LightingCapabilities.Brightness : info.Capabilities & ~LightingCapabilities.Brightness;
				}
			}

			{
				var result = await deviceConfigurationContainer.ReadValueAsync(SourceGenerationContext.Default.PersistedLightingDeviceConfiguration, cancellationToken).ConfigureAwait(false);
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
					var result = await lightingZoneConfigurationConfigurationContainer.ReadValueAsync(lightingZoneId, SourceGenerationContext.Default.PersistedLightingZoneInformation, cancellationToken).ConfigureAwait(false);
					if (result.Found)
					{
						var info = result.Value;
						state.SupportedEffectTypeIds = info.SupportedEffectTypeIds;
					}
				}
				if ((capabilities & LightingCapabilities.DeviceManagedLighting) == 0)
				{
					var result = await lightingZoneConfigurationConfigurationContainer.ReadValueAsync(lightingZoneId, SourceGenerationContext.Default.LightingEffect, cancellationToken).ConfigureAwait(false);
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
						persistenceMode,
						capabilities,
						unifiedLightingZoneId.GetValueOrDefault(),
						lightingZones
					)
					{
						IsUnifiedLightingEnabled = isUnifiedLightingEnabled,
						Brightness = brightness,
					}
				);
			}
		}

		return new LightingService
		(
			logger,
			lightingConfigurationContainer,
			devicesConfigurationContainer,
			deviceWatcher,
			powerNotificationService,
			useCentralizedLighting,
			centralizedLightingEffects,
			deviceStates
		);
	}

	private readonly DeviceRegistry _deviceWatcher;
	private readonly ConcurrentDictionary<Guid, DeviceState> _lightingDeviceStates;
	private readonly IConfigurationContainer _lightingConfigurationContainer;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly Lock _changeLock;
	private readonly AsyncLock _restoreLock;
	private TaskCompletionSource _restoreTaskCompletionSource;
	private ChangeBroadcaster<LightingDeviceInformation> _deviceInformationBroadcaster;
	private ChangeBroadcaster<LightingDeviceConfiguration> _deviceConfigurationBroadcaster;
	private ChangeBroadcaster<LightingConfiguration> _configurationBroadcaster;
	private ChangeBroadcaster<DisconnectedLightingDeviceInformation> _disconnectedDeviceBroadcaster;
	private readonly EffectChangeHandler _effectChangeHandler;
	private readonly BrightnessChangeHandler _brightnessChangeHandler;

	// Setting a global effect will involve fallbacks in order to cover all devices.
	// The fallbacks can always be programmatically computed from the first effect, but it is easier to just store all of them here.
	private LightingEffect[] _centralizedEffects;
	private bool _useCentralizedLighting;

	private readonly ILogger<LightingService> _logger;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _watchTask;
	private readonly Task _watchSuspendResumeTask;
	private readonly IDisposable _powerNotificationsRegistration;

	private LightingService
	(
		ILogger<LightingService> logger,
		IConfigurationContainer lightingConfigurationContainer,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		DeviceRegistry deviceWatcher,
		IPowerNotificationService powerNotificationService,
		bool useCentralizedLighting,
		LightingEffect[] centralizedEffects,
		ConcurrentDictionary<Guid, DeviceState> lightingDeviceStates
	)
	{
		_logger = logger;
		_lightingConfigurationContainer = lightingConfigurationContainer;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_deviceWatcher = deviceWatcher;
		_lightingDeviceStates = lightingDeviceStates;
		_changeLock = new();
		_restoreLock = new();
		_useCentralizedLighting = useCentralizedLighting;
		_centralizedEffects = centralizedEffects;
		_restoreTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_cancellationTokenSource = new();
		_effectChangeHandler = new(OnEffectChanged);
		_brightnessChangeHandler = new(OnBrightnessChanged);
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
		_watchSuspendResumeTask = WatchSuspendResumeAsync(_cancellationTokenSource.Token);
		_powerNotificationsRegistration = powerNotificationService.Register(this, PowerSettings.None);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			_restoreTaskCompletionSource.TrySetCanceled(cts.Token);
			await _watchTask.ConfigureAwait(false);
			await _watchSuspendResumeTask.ConfigureAwait(false);
			_powerNotificationsRegistration.Dispose();
			cts.Dispose();
		}
	}

	// Retrieves the global cancellation token while checking that the instance is not disposed.
	// We use this cancellation token to cancel pending write operations.
	private CancellationToken GetCancellationToken()
	{
		var cts = Volatile.Read(ref _cancellationTokenSource);
		ObjectDisposedException.ThrowIf(cts is null, typeof(LightingService));
		return cts.Token;
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ILightingDeviceFeature>(cancellationToken))
			{
				using (await _restoreLock.WaitAsync(cancellationToken).ConfigureAwait(false))
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
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task WatchSuspendResumeAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _restoreTaskCompletionSource.Task.ConfigureAwait(false);
			Volatile.Write(ref _restoreTaskCompletionSource, new(TaskCreationOptions.RunContinuationsAsynchronously));
			using (await _restoreLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				bool isGlobalLighting;
				LightingEffect[]? globalEffects;
				lock (_changeLock)
				{
					isGlobalLighting = _useCentralizedLighting;
					globalEffects = _centralizedEffects;
				}
				await RestoreEffectsAsync(isGlobalLighting ? globalEffects : null, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task RestoreEffectsAsync(LightingEffect[]? globalEffects, CancellationToken cancellationToken)
	{
		List<ValueTask>? applyChangeTasks = null;
		foreach (var (deviceId, deviceState) in _lightingDeviceStates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			lock (deviceState.Lock)
			{
				if (deviceState.Driver is null) continue;

				// TODO: Optimize redundant condition checks (using goto, probably 😄)
				if (deviceState.IsUnifiedLightingEnabled || globalEffects is not null && deviceState.UnifiedLightingZoneId != default)
				{
					var zoneState = deviceState.LightingZones[deviceState.UnifiedLightingZoneId];
					if (zoneState.LightingZone is null) continue;
					if (globalEffects is not null)
					{
						EffectSerializer.TrySetEffect(zoneState.LightingZone, globalEffects);
					}
					else
					{
						if (zoneState.SerializedCurrentEffect is null) continue;
						EffectSerializer.TrySetEffect(zoneState.LightingZone, zoneState.SerializedCurrentEffect);
					}
				}
				else if (globalEffects is not null)
				{
					foreach (var (zoneId, zoneState) in deviceState.LightingZones)
					{
						if (zoneId == deviceState.UnifiedLightingZoneId || zoneState.LightingZone is null) continue;
						EffectSerializer.TrySetEffect(zoneState.LightingZone, globalEffects);
					}
				}
				else
				{
					foreach (var (zoneId, zoneState) in deviceState.LightingZones)
					{
						if (zoneId == deviceState.UnifiedLightingZoneId || zoneState.LightingZone is null || zoneState.SerializedCurrentEffect is null) continue;
						EffectSerializer.TrySetEffect(zoneState.LightingZone, zoneState.SerializedCurrentEffect);
					}
				}

				var applyChangesTask = ApplyChangesAsync(deviceState.Driver.GetFeatureSet<ILightingDeviceFeature>(), false);
				if (!applyChangesTask.IsCompletedSuccessfully)
				{
					(applyChangeTasks ??= new()).Add(applyChangesTask);
				}
			}
		}
		if (applyChangeTasks is not null)
		{
			foreach (var applyChangeTask in applyChangeTasks)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					await applyChangeTask.ConfigureAwait(false);
				}
				catch (Exception)
				{
					// TODO: Log
				}
			}
		}
		applyChangeTasks = null;
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
			deviceState.PersistenceMode,
			deviceState.BrightnessCapabilities,
			null,
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
		BrightnessCapabilities brightnessCapabilities = default;
		Tuple<Type[], Guid[]>? supportedEffectsAndIds = null;
		byte? brightness = null;
		bool isUnifiedLightingEnabled = false;
		LightingPersistenceMode persistenceMode = LightingPersistenceMode.NeverPersisted;
		LightingCapabilities capabilities = LightingCapabilities.None;

		var lightingFeatures = (IDeviceFeatureSet<ILightingDeviceFeature>)notification.FeatureSet!;

		var lightingControllerFeature = lightingFeatures.GetFeature<ILightingControllerFeature>();
		var lightingZones = lightingControllerFeature?.LightingZones ?? [];

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

		var persistenceFeature = lightingFeatures.GetFeature<ILightingPersistenceFeature>();
		if (persistenceFeature is not null)
		{
			persistenceMode = persistenceFeature.PersistenceMode;
			if (persistenceFeature.HasDeviceManagedLighting) capabilities |= LightingCapabilities.DeviceManagedLighting;
			if (persistenceFeature.HasDynamicPresence) capabilities |= LightingCapabilities.DynamicPresence;
		}

		var brightnessFeature = lightingFeatures.GetFeature<ILightingBrightnessFeature>();
		if (brightnessFeature is not null)
		{
			brightnessCapabilities = new(brightnessFeature.MinimumBrightness, brightnessFeature.MaximumBrightness);
			brightness = brightnessFeature.CurrentBrightness;
			capabilities |= LightingCapabilities.Brightness;
		}

		var dynamicEffectChangesFeature = lightingFeatures.GetFeature<ILightingDynamicEffectChanges>();
		if (dynamicEffectChangesFeature is not null)
		{
			dynamicEffectChangesFeature.EffectChanged += _effectChangeHandler;
			capabilities |= LightingCapabilities.DynamicEffectChanges;
		}

		var dynamicBrightnessChangesFeature = lightingFeatures.GetFeature<ILightingDynamicBrightnessChanges>();
		if (dynamicBrightnessChangesFeature is not null)
		{
			dynamicBrightnessChangesFeature.BrightnessChanged += _brightnessChangeHandler;
			capabilities |= LightingCapabilities.DynamicBrightnessChanges;
		}

		var changedLightingZones = new HashSet<Guid>();

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
				lightingZoneStates.Add
				(
					unifiedLightingZoneId,
					new()
					{
						SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2),
						LightingZone = unifiedLightingFeature,
						SerializedCurrentEffect = isUnifiedLightingEnabled ? EffectSerializer.GetEffect(unifiedLightingFeature) : null,
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
				lightingZoneStates.Add
				(
					lightingZoneId,
					new()
					{
						SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2),
						LightingZone = lightingZone,
						SerializedCurrentEffect = isUnifiedLightingEnabled ? null : EffectSerializer.GetEffect(lightingZone),
					}
				);
			}

			deviceState = new(deviceContainer, lightingZonesContainer, brightnessCapabilities, persistenceMode, capabilities, unifiedLightingZoneId, lightingZoneStates)
			{
				Driver = notification.Driver,
				IsUnifiedLightingEnabled = isUnifiedLightingEnabled,
				Brightness = brightness
			};

			await deviceState.DeviceConfigurationContainer.WriteValueAsync
			(
				new PersistedLightingDeviceInformation
				(
					(capabilities & LightingCapabilities.Brightness) != 0 ? brightnessCapabilities : null,
					unifiedLightingFeature is not null ? unifiedLightingZoneId : null,
					persistenceMode,
					capabilities & ~LightingCapabilities.Brightness
				),
				SourceGenerationContext.Default.PersistedLightingDeviceInformation,
				cancellationToken
			).ConfigureAwait(false);

			await PersistDeviceConfigurationAsync
			(
				deviceState.DeviceConfigurationContainer,
				deviceState.CreatePersistedConfiguration(),
				cancellationToken
			).ConfigureAwait(false);

			foreach (var kvp in lightingZoneStates)
			{
				await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
				(
					kvp.Key,
					new PersistedLightingZoneInformation { SupportedEffectTypeIds = kvp.Value.SupportedEffectTypeIds },
					SourceGenerationContext.Default.PersistedLightingZoneInformation,
					cancellationToken
				).ConfigureAwait(false);

				if (kvp.Value.SerializedCurrentEffect is { } effect)
				{
					await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
					(
						kvp.Key,
						effect,
						SourceGenerationContext.Default.LightingEffect,
						cancellationToken
					).ConfigureAwait(false);
				}
			}

			// For a new device, we generally don't push any explicit change (it will stay in it initial state, driver-dependent)
			// However, when centralized lighting is enabled, we will always try to apply the effect on the device.
			if (_useCentralizedLighting)
			{
				if (unifiedLightingFeature is not null)
				{
					shouldApplyChanges |= EffectSerializer.TrySetEffect(unifiedLightingFeature, _centralizedEffects);
				}
				else
				{
					foreach (var lightingZone in lightingZones)
					{
						shouldApplyChanges |= EffectSerializer.TrySetEffect(lightingZone, _centralizedEffects);
					}
				}
			}

			lock (_changeLock)
			{
				_lightingDeviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);

				// Handlers can only be added from within the lock, so we can conditionally emit the new notifications based on the needs. (Handlers can be removed at anytime)
				var deviceInformationBroadcaster = _deviceInformationBroadcaster.GetSnapshot();
				if (!deviceInformationBroadcaster.IsEmpty) deviceInformationBroadcaster.Push(CreateDeviceInformation(notification.DeviceInformation.Id, deviceState));
				var deviceConfigurationBroadcaster = _deviceConfigurationBroadcaster.GetSnapshot();
				if (!deviceConfigurationBroadcaster.IsEmpty) deviceConfigurationBroadcaster.Push(deviceState.CreateConfiguration(notification.DeviceInformation.Id));

				if (shouldApplyChanges)
				{
					if (lightingFeatures.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
					{
						applyChangesTask = ApplyRestoredChangesAsync(dcf, notification.DeviceInformation.Id);
					}
				}
			}
		}
		else
		{
			bool shouldUpdateDeviceInformation = false;
			bool shouldUpdateDeviceConfiguration = false;

			lightingZoneStates = deviceState.LightingZones;

			var oldLightingZones = new HashSet<Guid>();
			if (brightnessFeature is not null && deviceState.Brightness is not null)
			{
				byte clampedBrightness = Math.Clamp(deviceState.Brightness.GetValueOrDefault(), deviceState.BrightnessCapabilities.MinimumValue, deviceState.BrightnessCapabilities.MaximumValue);
				if (clampedBrightness != deviceState.Brightness.GetValueOrDefault()) brightness = clampedBrightness;
				SetBrightness(notification.DeviceInformation.Id, clampedBrightness, true);
				shouldApplyChanges = true;
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
			}

			// After the previous steps, reset the HashSet and start listing the changed lighting zones instead.
			changedLightingZones.Clear();

			// After the steps above, we know for sure that there isn't any conflict with lighting zone IDs, and we can start killing old states.
			// First, we remove the configuration, outside the device state lock.
			foreach (var oldLightingZoneId in oldLightingZones)
			{
				await deviceState.LightingZonesConfigurationContainer.DeleteValuesAsync(oldLightingZoneId).ConfigureAwait(false);
			}


			lock (_changeLock)
			{
				bool shouldRestore = (capabilities & LightingCapabilities.DeviceManagedLighting) == 0;

				lock (deviceState.Lock)
				{
					// Within the lock, remove old lighting zones.
					foreach (var oldLightingZoneId in oldLightingZones)
					{
						deviceState.LightingZones.Remove(oldLightingZoneId);
					}

					if (brightnessFeature is not null && deviceState.Brightness is { } oldBrightness)
					{
						if (shouldRestore && deviceState.Brightness is not null)
						{
							brightnessFeature.CurrentBrightness = oldBrightness;
							shouldApplyChanges = true;
						}
						else
						{
							deviceState.Brightness = brightness;
						}
					}

					if (unifiedLightingFeature is not null)
					{
						if (lightingZoneStates.TryGetValue(unifiedLightingZoneId, out var lightingZoneState))
						{
							// NB: We mainly want to restore the existing configuration, so if the zone is found in the persisted configuration (which is expected 99% of the time),
							// then the unified lighting state from the device should be ignored, and we will apply the persisted value.
							isUnifiedLightingEnabled = deviceState.IsUnifiedLightingEnabled;

							lightingZoneState.LightingZone = unifiedLightingFeature;
							UpdateSupportedEffects(unifiedLightingZoneId, lightingZoneState, unifiedLightingFeature.GetType(), changedLightingZones);
							var currentEffect = EffectSerializer.GetEffect(unifiedLightingFeature);
							if (shouldRestore)
							{
								// We restore the effect from the saved state if available.
								if (lightingZoneState.SerializedCurrentEffect is { } effect)
								{
									if (isUnifiedLightingEnabled)
									{
										if (_useCentralizedLighting)
										{
											shouldApplyChanges |= EffectSerializer.TrySetEffect(unifiedLightingFeature, _centralizedEffects);
										}
										else if (effect != currentEffect)
										{
											shouldApplyChanges |= EffectSerializer.TrySetEffect(unifiedLightingFeature, effect);
										}
									}
								}
								else
								{
									lightingZoneState.SerializedCurrentEffect = currentEffect;
									changedLightingZones.Add(unifiedLightingZoneId);

									if (_useCentralizedLighting)
									{
										shouldApplyChanges |= EffectSerializer.TrySetEffect(unifiedLightingFeature, _centralizedEffects);
									}
								}
							}
							else if (currentEffect != lightingZoneState.SerializedCurrentEffect)
							{
								lightingZoneState.SerializedCurrentEffect = currentEffect;
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
									SerializedCurrentEffect = EffectSerializer.GetEffect(unifiedLightingFeature)
								}
							);
							changedLightingZones.Add(unifiedLightingZoneId);

							if (_useCentralizedLighting)
							{
								shouldApplyChanges |= EffectSerializer.TrySetEffect(unifiedLightingFeature, _centralizedEffects);
							}
						}
					}

					foreach (var lightingZone in lightingZones)
					{
						lightingZoneId = lightingZone.ZoneId;

						if (lightingZoneStates.TryGetValue(lightingZoneId, out var lightingZoneState))
						{
							lightingZoneState.LightingZone = lightingZone;
							UpdateSupportedEffects(lightingZone.ZoneId, lightingZoneState, lightingZone.GetType(), changedLightingZones);
							var currentEffect = EffectSerializer.GetEffect(lightingZone);
							if (shouldRestore)
							{
								// We restore the effect from the saved state if available.
								if (lightingZoneState.SerializedCurrentEffect is { } effect)
								{
									if (!isUnifiedLightingEnabled)
									{
										if (_useCentralizedLighting)
										{
											shouldApplyChanges |= EffectSerializer.TrySetEffect(lightingZone, _centralizedEffects);
										}
										else if (effect != currentEffect)
										{
											EffectSerializer.TrySetEffect(lightingZone, effect);
											shouldApplyChanges = true;
										}
									}
								}
								else
								{
									lightingZoneState.SerializedCurrentEffect = EffectSerializer.GetEffect(lightingZone);
									changedLightingZones.Add(lightingZoneId);

									if (_useCentralizedLighting)
									{
										shouldApplyChanges |= EffectSerializer.TrySetEffect(lightingZone, _centralizedEffects);
									}
								}
							}
							else if (currentEffect != lightingZoneState.SerializedCurrentEffect)
							{
								lightingZoneState.SerializedCurrentEffect = currentEffect;
								changedLightingZones.Add(unifiedLightingZoneId);
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
									SerializedCurrentEffect = EffectSerializer.GetEffect(lightingZone)
								}
							);
							changedLightingZones.Add(lightingZoneId);

							if (_useCentralizedLighting)
							{
								shouldApplyChanges |= EffectSerializer.TrySetEffect(lightingZone, _centralizedEffects);
							}
						}
					}

					if (deviceState.PersistenceMode != persistenceMode ||
						deviceState.UnifiedLightingZoneId != unifiedLightingZoneId ||
						deviceState.BrightnessCapabilities != brightnessCapabilities ||
						deviceState.Capabilities != capabilities)
					{
						deviceState.PersistenceMode = persistenceMode;
						deviceState.UnifiedLightingZoneId = unifiedLightingZoneId;
						deviceState.BrightnessCapabilities = brightnessCapabilities;
						deviceState.Capabilities = capabilities;
						shouldUpdateDeviceInformation = true;
					}
					if (deviceState.IsUnifiedLightingEnabled != isUnifiedLightingEnabled || deviceState.Brightness != brightness)
					{
						deviceState.IsUnifiedLightingEnabled = isUnifiedLightingEnabled;
						deviceState.Brightness = brightness;
						shouldUpdateDeviceConfiguration = true;
					}
					deviceState.Driver = notification.Driver;

					if (shouldApplyChanges)
					{
						if (lightingFeatures.GetFeature<ILightingDeferredChangesFeature>() is { } dcf)
						{
							applyChangesTask = ApplyRestoredChangesAsync(dcf, notification.DeviceInformation.Id);
						}
					}

					// Handlers can only be added from within the lock, so we can conditionally emit the new notifications based on the needs. (Handlers can be removed at anytime)
					var deviceInformationBroadcaster = _deviceInformationBroadcaster.GetSnapshot();
					if (!deviceInformationBroadcaster.IsEmpty) deviceInformationBroadcaster.Push(CreateDeviceInformation(notification.DeviceInformation.Id, deviceState));
					var deviceConfigurationBroadcaster = _deviceConfigurationBroadcaster.GetSnapshot();
					if (!deviceConfigurationBroadcaster.IsEmpty) deviceConfigurationBroadcaster.Push(deviceState.CreateConfiguration(notification.DeviceInformation.Id));
				}
			}

			if (shouldUpdateDeviceInformation)
			{
				await deviceState.DeviceConfigurationContainer.WriteValueAsync
				(
					new PersistedLightingDeviceInformation
					(
						(capabilities & LightingCapabilities.Brightness) != 0 ? brightnessCapabilities : null,
						unifiedLightingFeature is not null ? unifiedLightingZoneId : null,
						persistenceMode,
						capabilities & ~LightingCapabilities.Brightness
					),
					SourceGenerationContext.Default.PersistedLightingDeviceInformation,
					cancellationToken
				).ConfigureAwait(false);
			}
			if (shouldUpdateDeviceConfiguration)
			{
				await PersistDeviceConfigurationAsync(deviceState.DeviceConfigurationContainer, deviceState.CreatePersistedConfiguration(), cancellationToken).ConfigureAwait(false);
			}

			foreach (var changedLightingZoneKey in changedLightingZones)
			{
				if (deviceState.LightingZones.TryGetValue(changedLightingZoneKey, out var changedLightingZone))
				{
					await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
					(
						changedLightingZoneKey,
						new PersistedLightingZoneInformation { SupportedEffectTypeIds = changedLightingZone.SupportedEffectTypeIds },
						SourceGenerationContext.Default.PersistedLightingZoneInformation,
						cancellationToken
					).ConfigureAwait(false);

					if (changedLightingZone.SerializedCurrentEffect is { } effect)
					{
						await deviceState.LightingZonesConfigurationContainer.WriteValueAsync
						(
							changedLightingZoneKey,
							effect,
							SourceGenerationContext.Default.LightingEffect,
							cancellationToken
						).ConfigureAwait(false);
					}
				}
			}
		}

		await applyChangesTask.ConfigureAwait(false);
	}

	private static void UpdateSupportedEffects(Guid unifiedLightingZoneId, LightingZoneState lightingZoneState, Type lightingZoneType, HashSet<Guid> changedLightingZones)
	{
		var supportedEffectsAndIds = GetSupportedEffects(lightingZoneType);
		// If the effects reference is exactly the same, we can skip everything and do nothing.
		if (ReferenceEquals(ImmutableCollectionsMarshal.AsArray(lightingZoneState.SupportedEffectTypeIds), supportedEffectsAndIds.Item2)) return;
		// Otherwise, if the list of supported effects has changed, the lighting zone must be updated.
		if (!lightingZoneState.SupportedEffectTypeIds.AsSpan().SequenceEqual(supportedEffectsAndIds.Item2))
		{
			// Mark that this lighting zone must be persisted again.
			changedLightingZones.Add(unifiedLightingZoneId);
		}
		// Always update the effect reference. It the contents were equal, it will let us get rid of the old copy, and only keep the one that was retrieved from the cache.
		lightingZoneState.SupportedEffectTypeIds = ImmutableCollectionsMarshal.AsImmutableArray(supportedEffectsAndIds.Item2);
	}

	private async ValueTask ApplyRestoredChangesAsync(ILightingDeferredChangesFeature deferredChangesFeature, Guid deviceId)
	{
		try
		{
			await deferredChangesFeature.ApplyChangesAsync(false).ConfigureAwait(false);
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
			lock (deviceState.Lock)
			{
				// Try to unregister the effect change handler first so as to avoid processing notifications while we are clearing up the state.
				// As long as the device signals that it manages its own lighting, we won't restore the state if it reconnects later on.
				if ((deviceState.Capabilities & LightingCapabilities.DynamicEffectChanges) != 0 &&
					deviceState.Driver!.GetFeatureSet<ILightingDeviceFeature>().GetFeature<ILightingDynamicEffectChanges>() is { } dynamicEffectChangesFeature)
				{
					dynamicEffectChangesFeature.EffectChanged -= _effectChangeHandler;
				}
				if ((deviceState.Capabilities & LightingCapabilities.DynamicBrightnessChanges) != 0 &&
					deviceState.Driver!.GetFeatureSet<ILightingDeviceFeature>().GetFeature<ILightingDynamicBrightnessChanges>() is { } dynamicBrightnessChangesFeature)
				{
					dynamicBrightnessChangesFeature.BrightnessChanged -= _brightnessChangeHandler;
				}
				foreach (var lightingZoneState in deviceState.LightingZones.Values)
				{
					Volatile.Write(ref lightingZoneState.LightingZone, null);
				}
				if ((deviceState.Capabilities & LightingCapabilities.DynamicPresence) != 0)
				{
					_disconnectedDeviceBroadcaster.Push(new(notification.DeviceInformation.Id));
				}
			}
		}
	}

	private void OnEffectChanged(Driver driver, ILightingZone zone, ILightingEffect effect)
	{
		if (zone is not null &&
			_deviceWatcher.TryGetDeviceId(driver, out var deviceId) &&
			_lightingDeviceStates.TryGetValue(deviceId, out var deviceState) &&
			deviceState.LightingZones.TryGetValue(zone.ZoneId, out var lightingZoneState))
		{
			lock (deviceState.Lock)
			{
				lightingZoneState.SerializedCurrentEffect = EffectSerializer.GetEffect(effect);
				UnsafeNotifyDeviceConfiguration(deviceId, deviceState);
			}
		}
	}

	private void OnBrightnessChanged(Driver driver, byte brightness)
	{
		if (_deviceWatcher.TryGetDeviceId(driver, out var deviceId) &&
			_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			lock (deviceState.Lock)
			{
				deviceState.Brightness = brightness;
				UnsafeNotifyDeviceConfiguration(deviceId, deviceState);
			}
		}
	}

	ValueTask<LightingDeviceInformation[]?> IChangeSource<LightingDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightingDeviceInformation> writer, CancellationToken cancellationToken)
	{
		var initialNotifications = new List<LightingDeviceInformation>();
		lock (_changeLock)
		{
			foreach (var kvp in _lightingDeviceStates)
			{
				if (kvp.Value.Driver is not null)
				{
					initialNotifications.Add(CreateDeviceInformation(kvp.Key, kvp.Value));
				}
			}
			_deviceInformationBroadcaster.Register(writer);
		}
		return new([.. initialNotifications]);
	}

	void IChangeSource<LightingDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<LightingDeviceInformation> writer)
		=> _deviceInformationBroadcaster.Unregister(writer);

	ValueTask IChangeSource<LightingDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<LightingDeviceInformation> writer)
	{
		lock (_changeLock)
		{
			_deviceInformationBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<LightingDeviceConfiguration[]?> IChangeSource<LightingDeviceConfiguration>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightingDeviceConfiguration> writer, CancellationToken cancellationToken)
	{
		var initialNotifications = new List<LightingDeviceConfiguration>();
		lock (_changeLock)
		{
			foreach (var kvp in _lightingDeviceStates)
			{
				if (kvp.Value.Driver is not null)
				{
					lock (kvp.Value.Lock)
					{
						initialNotifications.Add(kvp.Value.CreateConfiguration(kvp.Key));
					}
				}
			}
			_deviceConfigurationBroadcaster.Register(writer);
		}
		return new([.. initialNotifications]);
	}

	void IChangeSource<LightingDeviceConfiguration>.UnsafeUnregisterWatcher(ChannelWriter<LightingDeviceConfiguration> writer)
		=> _deviceConfigurationBroadcaster.Unregister(writer);

	ValueTask IChangeSource<LightingDeviceConfiguration>.SafeUnregisterWatcherAsync(ChannelWriter<LightingDeviceConfiguration> writer)
	{
		lock (_changeLock)
		{
			_deviceConfigurationBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<LightingConfiguration[]?> IChangeSource<LightingConfiguration>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<LightingConfiguration> writer, CancellationToken cancellationToken)
	{
		LightingConfiguration[] configuration;
		lock (_changeLock)
		{
			configuration = [new(_useCentralizedLighting, _centralizedEffects[0])];
			_configurationBroadcaster.Register(writer);
		}
		return new(configuration);
	}

	void IChangeSource<LightingConfiguration>.UnsafeUnregisterWatcher(ChannelWriter<LightingConfiguration> writer)
		=> _configurationBroadcaster.Unregister(writer);

	ValueTask IChangeSource<LightingConfiguration>.SafeUnregisterWatcherAsync(ChannelWriter<LightingConfiguration> writer)
	{
		lock (_changeLock)
		{
			_configurationBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<DisconnectedLightingDeviceInformation[]?> IChangeSource<DisconnectedLightingDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<DisconnectedLightingDeviceInformation> writer, CancellationToken cancellationToken)
	{
		lock (_changeLock)
		{
			_disconnectedDeviceBroadcaster.Register(writer);
		}
		return new(null as DisconnectedLightingDeviceInformation[]);
	}

	void IChangeSource<DisconnectedLightingDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<DisconnectedLightingDeviceInformation> writer)
		=> _disconnectedDeviceBroadcaster.Unregister(writer);

	ValueTask IChangeSource<DisconnectedLightingDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<DisconnectedLightingDeviceInformation> writer)
	{
		lock (_changeLock)
		{
			_disconnectedDeviceBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
		return ValueTask.CompletedTask;
	}

	public void SetEffect(Guid deviceId, Guid zoneId, Guid effectId, ReadOnlySpan<byte> data)
	{
		var cancellationToken = GetCancellationToken();

		if (!_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			throw new DeviceNotFoundException();
		}

		lock (deviceState.Lock)
		{
			if (!deviceState.LightingZones.TryGetValue(zoneId, out var zoneState))
			{
				throw new LightingZoneNotFoundException(zoneId);
			}

			bool isUnifiedLightingZone = zoneId == deviceState.UnifiedLightingZoneId;
			bool isUnifiedLightingUpdated = deviceState.IsUnifiedLightingEnabled != isUnifiedLightingZone;
			bool hasEffectChanged = zoneState.SerializedCurrentEffect is null ||
				zoneState.SerializedCurrentEffect.EffectId != effectId ||
				!data.SequenceEqual(zoneState.SerializedCurrentEffect.EffectData);

			// Skip the actual update if the serialized data is already up-to-date.
			if (!hasEffectChanged && !isUnifiedLightingUpdated) return;

			if (zoneState.LightingZone is { } zone && !_useCentralizedLighting)
			{
				EffectSerializer.TrySetEffect(zone, effectId, data);
			}

			var effect = new LightingEffect(effectId, data.ToArray());
			zoneState.SerializedCurrentEffect = effect;

			// Skip the rest of the updates if global lighting is currently enabled.
			if (_useCentralizedLighting) return;

			if (isUnifiedLightingUpdated)
			{
				deviceState.IsUnifiedLightingEnabled = isUnifiedLightingZone;

				// When switching from unified lighting to non-unified lighting, all the other lighting zones need to be restored.
				// This will cause the lock to be re-entered, which is not something I personally like, but since we restored the unified lighting state above
				// and ensured that the operations are restore operations, there should not be more than one level of recursion here.
				if (!isUnifiedLightingZone)
				{
					foreach (var kvp in deviceState.LightingZones)
					{
						if (kvp.Key == deviceState.UnifiedLightingZoneId || kvp.Key == zoneId || kvp.Value.LightingZone is null || kvp.Value.SerializedCurrentEffect is null) continue;
						EffectSerializer.TrySetEffect(kvp.Value.LightingZone, kvp.Value.SerializedCurrentEffect);
					}
				}
			}

			PersistActiveEffect(deviceState.LightingZonesConfigurationContainer, zoneId, effect, cancellationToken);
			if (isUnifiedLightingUpdated)
			{
				PersistDeviceConfiguration(deviceState.DeviceConfigurationContainer, deviceState.CreatePersistedConfiguration(), cancellationToken);
			}
		}
	}

	// TODO: Find a way to merge this with the update logic, as it exists only because of that.
	// Probably just inline the whole protocol logic in this class although it would be a bit dirty. (It would nevertheless be more efficient, and the protocol is actually the only client as of now)
	public void NotifyDeviceConfiguration(Guid deviceId)
	{
		if (!_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			throw new DeviceNotFoundException();
		}

		NotifyDeviceConfiguration(deviceId, deviceState);
	}

	private void NotifyDeviceConfiguration(Guid deviceId, DeviceState deviceState)
	{
		lock (deviceState.Lock)
		{
			UnsafeNotifyDeviceConfiguration(deviceId, deviceState);
		}
	}

	private void UnsafeNotifyDeviceConfiguration(Guid deviceId, DeviceState deviceState)
	{
		var configurationBroadcaster = _deviceConfigurationBroadcaster.GetSnapshot();
		if (configurationBroadcaster.IsEmpty) return;
		configurationBroadcaster.Push(deviceState.CreateConfiguration(deviceId));
	}

	public void SetBrightness(Guid deviceId, byte brightness) => SetBrightness(deviceId, brightness, false);

	private void SetBrightness(Guid deviceId, byte brightness, bool isRestore)
	{
		var cancellationToken = GetCancellationToken();

		if (!_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			throw new DeviceNotFoundException();
		}

		lock (deviceState.Lock)
		{
			if ((deviceState.Capabilities & LightingCapabilities.Brightness) == 0 || brightness == deviceState.Brightness) return;

			if (!isRestore)
			{
				deviceState.Brightness = brightness;
			}

			if (deviceState.Driver is not null)
			{
				var lightingFeatures = deviceState.Driver.GetFeatureSet<ILightingDeviceFeature>();

				if (lightingFeatures.GetFeature<ILightingBrightnessFeature>() is { } bf)
				{
					bf.CurrentBrightness = brightness;
				}
			}
		}

		if (!isRestore)
		{
			PersistDeviceConfiguration(deviceState.DeviceConfigurationContainer, deviceState.CreatePersistedConfiguration(), cancellationToken);
		}
	}

	public ValueTask ApplyChangesAsync(Guid deviceId, bool shouldPersist)
	{
		ValueTask applyChangesTask = ValueTask.CompletedTask;

		if (!_lightingDeviceStates.TryGetValue(deviceId, out var deviceState))
		{
			return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new DeviceNotFoundException()));
		}

		lock (deviceState.Lock)
		{
			if (deviceState.Driver is null) goto Completed;

			applyChangesTask = ApplyChangesAsync(deviceState.Driver.GetFeatureSet<ILightingDeviceFeature>(), shouldPersist);
		}

	Completed:;
		return applyChangesTask;
	}

	public ReadOnlySpan<Guid> GetSupportedCentralizedEffects() => SupportedCentralizedEffects;

	public async Task SetLightingConfigurationAsync(bool useCentralizedLighting, LightingEffect? effect, CancellationToken cancellationToken)
	{
		using (await _restoreLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			bool isChanged = false;
			bool isEffectChanged = false;
			lock (_changeLock)
			{
				if (effect is not null)
				{
					if (_centralizedEffects is not { Length: > 0 } || _centralizedEffects[0] != effect)
					{
						_centralizedEffects = BuildEffectFallbacks(effect);
						isChanged = isEffectChanged = true;
					}
				}

				if (useCentralizedLighting != _useCentralizedLighting)
				{
					_useCentralizedLighting = useCentralizedLighting;
					isChanged = true;
				}

				if (isChanged)
				{
					_configurationBroadcaster.Push(new(_useCentralizedLighting, isEffectChanged ? _centralizedEffects[0] : null));
				}
			}

			if (isChanged)
			{
				var configurationPersistenceTask = PersistConfigurationAsync
				(
					_lightingConfigurationContainer,
					new()
					{
						IsCentralizedLightingEnabled = _useCentralizedLighting,
						CentralizedLightingEffect = _centralizedEffects[0]
					},
					cancellationToken
				);
				try
				{
					await RestoreEffectsAsync(_useCentralizedLighting ? _centralizedEffects : null, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					try
					{
						await configurationPersistenceTask.ConfigureAwait(false);
					}
					catch
					{
						// TODO: Log
					}
				}
			}
		}
	}

	private static LightingEffect[] BuildEffectFallbacks(LightingEffect effect)
	{
		if (effect.EffectId == DisabledEffectId)
		{
			return [EffectSerializer.Serialize(new DisabledEffect())];
		}
		else if (effect.EffectId == StaticColorEffectId)
		{
			var staticColorEffect = EffectSerializer.UnsafeDeserialize<StaticColorEffect>(effect.EffectData);
			var color = ColorSpaceConverter.ToLinearRgb(new ImageSharpRgb(staticColorEffect.Color.R, staticColorEffect.Color.G, staticColorEffect.Color.B));
			float equivalentBrightness = 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
			return
			[
				EffectSerializer.Serialize(staticColorEffect),
				// TODO: Solve the brightness problem. We should have a uniform way to represent brightness across devices but also allow device-specific representations.
				// The main problem is that the discrete brightness values supported by a device can vary across devices.
				// For now, the code below assumes that brightness ranges from 0 to 100, which is wrong.
				// Maybe splitting brightness in two flavors would be enough, but in some cases, being able to read back the actual brightness from the device might be necessary.
				EffectSerializer.Serialize(new StaticBrightnessEffect((byte)(RgbWorkingSpaces.SRgb.Compress(equivalentBrightness) * 100))),
				equivalentBrightness >= 0.5f ? EffectSerializer.Serialize(new EnabledEffect()) : EffectSerializer.Serialize(new DisabledEffect()),
				EffectSerializer.Serialize(new DisabledEffect()),
			];
		}
		else if (effect.EffectId == SpectrumCycleEffectId)
		{
			return
			[
				EffectSerializer.Serialize(new SpectrumCycleEffect()),
				EffectSerializer.Serialize(new SpectrumWaveEffect()),
				EffectSerializer.Serialize(new StaticColorEffect(new(255, 255, 255))),
				EffectSerializer.Serialize(new EnabledEffect()),
				EffectSerializer.Serialize(new DisabledEffect()),
			];
		}
		else if (effect.EffectId == SpectrumWaveEffectId)
		{
			return
			[
				EffectSerializer.Serialize(new SpectrumWaveEffect()),
				EffectSerializer.Serialize(new SpectrumCycleEffect()),
				EffectSerializer.Serialize(new StaticColorEffect(new(255, 255, 255))),
				EffectSerializer.Serialize(new StaticBrightnessEffect(255)),
				EffectSerializer.Serialize(new EnabledEffect()),
				EffectSerializer.Serialize(new DisabledEffect()),
			];
		}
		else
		{
			throw new ArgumentException("Unsupported effect.");
		}
	}

	private static async ValueTask ApplyChangesAsync(IDeviceFeatureSet<ILightingDeviceFeature> lightingFeatures, bool shouldPersist)
	{
		if (lightingFeatures.GetFeature<ILightingDeferredChangesFeature>() is { } deferredChangesFeature)
		{
			await deferredChangesFeature.ApplyChangesAsync(shouldPersist).ConfigureAwait(false);
		}
	}

	// NB: With the current code, there is not a strong enforcing of configuration update order.
	// (Important to note, though, configuration writes themselves are already serialized using a lock. The worst that can happen is a later configuration being overwritten by an earlier one)
	// I don't think it matters too much as these configuration changes should not occur concurrently and they are supposed to be the result of manual actions of the user (so, in slow sequence).
	// The code should still be improved probably, as we don't prevent it, but it can be done later. (Especially considering we want to have a more complex programming model somewhat replacing this)

	private ValueTask PersistConfigurationAsync(IConfigurationContainer lightingConfigurationContainer, PersistedLightingConfiguration configuration, CancellationToken cancellationToken)
		=> lightingConfigurationContainer.WriteValueAsync(configuration, SourceGenerationContext.Default.PersistedLightingConfiguration, cancellationToken);

	private async void PersistActiveEffect(IConfigurationContainer<Guid> lightingZonesConfigurationContainer, Guid zoneId, LightingEffect effect, CancellationToken cancellationToken)
	{
		try
		{
			await PersistActiveEffectAsync(lightingZonesConfigurationContainer, zoneId, effect, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private ValueTask PersistActiveEffectAsync(IConfigurationContainer<Guid> lightingZonesConfigurationContainer, Guid zoneId, LightingEffect effect, CancellationToken cancellationToken)
		=> lightingZonesConfigurationContainer.WriteValueAsync(zoneId, effect, SourceGenerationContext.Default.LightingEffect, cancellationToken);

	private async void PersistDeviceConfiguration(IConfigurationContainer deviceConfigurationContainer, PersistedLightingDeviceConfiguration configuration, CancellationToken cancellationToken)
	{
		try
		{
			await PersistDeviceConfigurationAsync(deviceConfigurationContainer, configuration, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private ValueTask PersistDeviceConfigurationAsync(IConfigurationContainer deviceConfigurationContainer, PersistedLightingDeviceConfiguration configuration, CancellationToken cancellationToken)
		=> deviceConfigurationContainer.WriteValueAsync(configuration, SourceGenerationContext.Default.PersistedLightingDeviceConfiguration, cancellationToken);

	void IPowerNotificationSink.OnResumeAutomatic() => _restoreTaskCompletionSource.TrySetResult();
}
