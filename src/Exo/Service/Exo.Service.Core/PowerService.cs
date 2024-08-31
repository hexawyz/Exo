using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts;
using Exo.Features;
using Exo.Features.PowerManagement;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("Power")]
[TypeId(0x645A580F, 0xECE8, 0x44DA, 0x80, 0x71, 0xCE, 0xA8, 0xDF, 0xE3, 0x69, 0xDF)]
[Event<BatteryEventParameters>("DeviceConnected", 0x51BCE224, 0x0DA2, 0x4965, 0xB5, 0xBD, 0xAD, 0x71, 0x28, 0xD6, 0xA4, 0xE4)]
[Event<BatteryEventParameters>("ExternalPowerConnected", 0xFA10C2ED, 0x2842, 0x4AE2, 0x8F, 0x3F, 0x18, 0xCC, 0x1C, 0x05, 0x16, 0x75)]
[Event<BatteryEventParameters>("ExternalPowerDisconnected", 0xF8E9D6E6, 0xA21B, 0x45EC, 0x8E, 0xF4, 0xE5, 0x3A, 0x5A, 0x54, 0xEA, 0xF7)]
[Event<BatteryEventParameters>("ChargingComplete", 0x2B75EB8F, 0x8393, 0x43A4, 0xB4, 0xA4, 0x58, 0x35, 0x90, 0x4A, 0x84, 0xCF)]
[Event<BatteryEventParameters>("Error", 0x1D4EE59D, 0x3FE0, 0x45BC, 0x8F, 0xEB, 0x82, 0xE2, 0x45, 0x89, 0x32, 0x1B)]
[Event<BatteryEventParameters>("Discharging", 0x889E49AD, 0x2D35, 0x4D8A, 0xBE, 0x0E, 0xAD, 0x2A, 0x21, 0xB7, 0xF1, 0xB8)]
[Event<BatteryEventParameters>("Charging", 0x19687F99, 0x6A9B, 0x41FA, 0xAC, 0x91, 0xDF, 0xDA, 0x0A, 0xD7, 0xF7, 0xD3)]
internal sealed class PowerService : IAsyncDisposable
{
	[TypeId(0xF118B54F, 0xDA42, 0x4768, 0x98, 0x50, 0x80, 0x7C, 0xB7, 0x71, 0x36, 0x24)]
	private readonly struct PersistedPowerDeviceInformation
	{
		public PowerDeviceCapabilities Capabilities { get; init; }
		public TimeSpan MinimumIdleTime { get; init; }
		public TimeSpan MaximumIdleTime { get; init; }
	}

	private sealed class DeviceState
	{
		private readonly object _lock;
		private readonly PowerService _powerService;
		private readonly IConfigurationContainer _configurationContainer;
		private IBatteryStateDeviceFeature? _batteryFeatures;
		private ILowPowerModeBatteryThresholdFeature? _lowPowerModeBatteryThresholdFeature;
		private IIdleSleepTimerFeature? _idleSleepTimerFeature;
		private readonly Guid _deviceId;
		private bool _isConnected;
		private PowerDeviceCapabilities _capabilities;
		private TimeSpan _minimumIdleTime;
		private TimeSpan _maximumIdleTime;
		private BatteryState _batteryState;
		private TimeSpan _idleTime;
		private Half _lowPowerBatteryThreshold;

		public DeviceState(PowerService powerService, IConfigurationContainer configurationContainer, Guid deviceId, PersistedPowerDeviceInformation information)
		{
			_lock = new();
			_powerService = powerService;
			_configurationContainer = configurationContainer;
			_deviceId = deviceId;
			_capabilities = information.Capabilities;
			_minimumIdleTime = information.MinimumIdleTime;
			_maximumIdleTime = information.MaximumIdleTime;
		}

		public Guid DeviceId => _deviceId;
		public bool IsConnected => _isConnected;
		public bool HasBattery => (_capabilities & PowerDeviceCapabilities.HasBattery) != 0;
		public bool HasLowPowerBatteryThreshold => (_capabilities & PowerDeviceCapabilities.HasLowPowerBatteryThreshold) != 0;
		public bool HasIdleTimer => (_capabilities & PowerDeviceCapabilities.HasIdleTimer) != 0;

		public bool OnConnected
		(
			IBatteryStateDeviceFeature? batteryFeatures,
			ILowPowerModeBatteryThresholdFeature? lowPowerModeBatteryThresholdFeature,
			IIdleSleepTimerFeature? idleSleepTimerFeature
		)
		{
			bool hasChanged = false;
			lock (_lock)
			{
				if (_isConnected) throw new InvalidOperationException();

				_isConnected = true;

				PowerDeviceCapabilities capabilities = 0;

				if (batteryFeatures is not null) capabilities |= PowerDeviceCapabilities.HasBattery;
				if (lowPowerModeBatteryThresholdFeature is not null) capabilities |= PowerDeviceCapabilities.HasLowPowerBatteryThreshold;

				if (idleSleepTimerFeature is not null)
				{
					capabilities |= PowerDeviceCapabilities.HasIdleTimer;
					hasChanged |= _minimumIdleTime != idleSleepTimerFeature.MinimumIdleTime;
					_minimumIdleTime = idleSleepTimerFeature.MinimumIdleTime;
					hasChanged |= _maximumIdleTime != idleSleepTimerFeature.MaximumIdleTime;
					_maximumIdleTime = idleSleepTimerFeature.MaximumIdleTime;
				}

				hasChanged |= capabilities != _capabilities;

				_capabilities = capabilities;

				_batteryFeatures = batteryFeatures;
				_lowPowerModeBatteryThresholdFeature = lowPowerModeBatteryThresholdFeature;
				_idleSleepTimerFeature = idleSleepTimerFeature;

				_powerService.NotifyDeviceConnection(CreatePowerDeviceInformation());

				if (batteryFeatures is not null)
				{
					batteryFeatures.BatteryStateChanged += OnBatteryStateChanged;
					_batteryState = batteryFeatures.BatteryState;
					_powerService.NotifyBatteryStateChange(new(WatchNotificationKind.Addition, _deviceId, _batteryState, default));
				}

				if (lowPowerModeBatteryThresholdFeature is not null)
				{
					_lowPowerBatteryThreshold = lowPowerModeBatteryThresholdFeature.LowPowerThreshold;
					_powerService.NotifyLowPowerBatteryThreshold(new() { DeviceId = DeviceId, BatteryThreshold = lowPowerModeBatteryThresholdFeature.LowPowerThreshold });
				}

				if (idleSleepTimerFeature is not null)
				{
					_idleTime = idleSleepTimerFeature.IdleTime;
					_powerService.NotifyIdleSleepTimer(new() { DeviceId = DeviceId, IdleTime = idleSleepTimerFeature.IdleTime });
				}
			}
			return hasChanged;
		}

		public void OnDisconnected()
		{
			lock (_lock)
			{
				if (!_isConnected) throw new InvalidOperationException();

				if (_batteryFeatures is not null) _batteryFeatures.BatteryStateChanged -= OnBatteryStateChanged;

				_isConnected = false;

				_batteryFeatures = null;
				_lowPowerModeBatteryThresholdFeature = null;
				_idleSleepTimerFeature = null;

				_powerService.NotifyDeviceConnection(CreatePowerDeviceInformation());
			}
		}

		public async Task PersistInformationAsync(CancellationToken cancellationToken)
			=> await _configurationContainer.WriteValueAsync
			(
				new PersistedPowerDeviceInformation()
				{
					Capabilities = _capabilities,
					MinimumIdleTime = _minimumIdleTime,
					MaximumIdleTime = _maximumIdleTime
				},
				cancellationToken
			).ConfigureAwait(false);

		public PowerDeviceInformation CreatePowerDeviceInformation()
			=> new() { DeviceId = _deviceId, IsConnected = _isConnected, Capabilities = _capabilities, MinimumIdleTime = _minimumIdleTime, MaximumIdleTime = _maximumIdleTime };

		private void OnBatteryStateChanged(Driver driver, BatteryState state)
		{
			lock (_lock)
			{
				if (_batteryState != state)
				{
					var notification = new ChangeWatchNotification<Guid, BatteryState>(WatchNotificationKind.Update, _deviceId, state, _batteryState);
					_batteryState = state;
					_powerService.NotifyBatteryStateChange(notification);
				}
			}
		}

		public bool TryGetBatteryState(out BatteryState state)
		{
			lock (_lock)
			{
				if (HasBattery && IsConnected)
				{
					state = _batteryState;
					return true;
				}
			}
			state = default;
			return false;
		}

		public bool TryGetLowPowerBatteryThreshold(out Half threshold)
		{
			if (HasLowPowerBatteryThreshold && IsConnected)
			{
				threshold = _lowPowerBatteryThreshold;
				return true;
			}
			threshold = default;
			return false;
		}

		public bool TryGetIdleTime(out TimeSpan idleTime)
		{
			if (HasIdleTimer && IsConnected)
			{
				idleTime = _idleTime;
				return true;
			}
			idleTime = default;
			return false;
		}

		public async Task SetLowPowerModeBatteryThresholdAsync(Half batteryThreshold, CancellationToken cancellationToken)
		{
			if (batteryThreshold < Half.Zero || batteryThreshold > Half.One) throw new ArgumentOutOfRangeException(nameof(batteryThreshold));
			if (_lowPowerModeBatteryThresholdFeature is { } lowPowerModeBatteryThresholdFeature)
			{
				await lowPowerModeBatteryThresholdFeature.SetLowPowerBatteryThresholdAsync(batteryThreshold, cancellationToken).ConfigureAwait(false);
				_lowPowerBatteryThreshold = batteryThreshold;
				_powerService.NotifyLowPowerBatteryThreshold(new() { DeviceId = DeviceId, BatteryThreshold = batteryThreshold });
			}
		}

		public async Task SetIdleSleepTimerAsync(TimeSpan idleTime, CancellationToken cancellationToken)
		{
			if (_idleSleepTimerFeature is { } idleSleepTimerFeature)
			{
				await idleSleepTimerFeature.SetIdleTimeAsync(idleTime, cancellationToken).ConfigureAwait(false);
				_idleTime = idleTime;
				_powerService.NotifyIdleSleepTimer(new() { DeviceId = DeviceId, IdleTime = idleTime });
			}
		}
	}

	public static async ValueTask<PowerService> CreateAsync
	(
		ILogger<PowerService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ChannelWriter<Event> eventWriter,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		return new PowerService
		(
			logger,
			devicesConfigurationContainer,
			deviceWatcher,
			eventWriter,
			deviceIds.Length > 0 ?
				await ParseDevicesAsync(devicesConfigurationContainer, deviceIds, cancellationToken).ConfigureAwait(false) :
				[]
		);
	}

	private static async Task<List<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedPowerDeviceInformation PersistedInformation)>> ParseDevicesAsync
	(
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		Guid[] deviceIds,
		CancellationToken cancellationToken
	)
	{
		var deviceStates = new List<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedPowerDeviceInformation PersistedInformation)>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			var result = await deviceConfigurationContainer.ReadValueAsync<PersistedPowerDeviceInformation>(cancellationToken).ConfigureAwait(false);

			if (!result.Found) continue;

			deviceStates.Add((deviceId, deviceConfigurationContainer, result.Value));
		}

		return deviceStates;
	}

	public static readonly Guid BatteryDeviceConnectedEventGuid = new(0x51BCE224, 0x0DA2, 0x4965, 0xB5, 0xBD, 0xAD, 0x71, 0x28, 0xD6, 0xA4, 0xE4);
	public static readonly Guid BatteryExternalPowerConnectedEventGuid = new(0xFA10C2ED, 0x2842, 0x4AE2, 0x8F, 0x3F, 0x18, 0xCC, 0x1C, 0x05, 0x16, 0x75);
	public static readonly Guid BatteryExternalPowerDisconnectedEventGuid = new(0xF8E9D6E6, 0xA21B, 0x45EC, 0x8E, 0xF4, 0xE5, 0x3A, 0x5A, 0x54, 0xEA, 0xF7);
	public static readonly Guid BatteryChargingCompleteEventGuid = new(0x2B75EB8F, 0x8393, 0x43A4, 0xB4, 0xA4, 0x58, 0x35, 0x90, 0x4A, 0x84, 0xCF);
	public static readonly Guid BatteryErrorEventGuid = new(0x1D4EE59D, 0x3FE0, 0x45BC, 0x8F, 0xEB, 0x82, 0xE2, 0x45, 0x89, 0x32, 0x1B);
	public static readonly Guid BatteryChargingEventGuid = new(0x19687F99, 0x6A9B, 0x41FA, 0xAC, 0x91, 0xDF, 0xDA, 0x0A, 0xD7, 0xF7, 0xD3);
	public static readonly Guid BatteryDischargingEventGuid = new(0x889E49AD, 0x2D35, 0x4D8A, 0xBE, 0x0E, 0xAD, 0x2A, 0x21, 0xB7, 0xF1, 0xB8);

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private ChannelWriter<PowerDeviceInformation>[]? _deviceChangeListeners;
	private ChannelWriter<ChangeWatchNotification<Guid, BatteryState>>[]? _batteryChangeListeners;
	private ChannelWriter<PowerDeviceLowPowerBatteryThresholdNotification>[]? _lowPowerBatteryThresholdListeners;
	private ChannelWriter<PowerDeviceIdleSleepTimerNotification>[]? _idleTimerListeners;
	private readonly ChannelWriter<Event> _eventWriter;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	private PowerService
	(
		ILogger<PowerService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ChannelWriter<Event> eventWriter,
		IReadOnlyList<(Guid DeviceId, IConfigurationContainer DeviceConfigurationContainer, PersistedPowerDeviceInformation PersistedInformation)> deviceStates
	)
	{
		_deviceStates = new();
		if (deviceStates is not null)
		{
			foreach (var (deviceId, configurationContainer, persistedInformation) in deviceStates)
			{
				_deviceStates.TryAdd(deviceId, new(this, configurationContainer, deviceId, persistedInformation));
			}
		}
		_lock = new();
		_eventWriter = eventWriter;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(deviceWatcher, _cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_watchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	private async Task WatchAsync(IDeviceWatcher deviceWatcher, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in deviceWatcher.WatchAvailableAsync<IPowerManagementDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Addition:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						}
						break;
					case WatchNotificationKind.Removal:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							// NB: Removal should not be cancelled. We need all the states to be cleared away.
							HandleRemoval(notification);
						}
						break;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		var powerFeatures = (IDeviceFeatureSet<IPowerManagementDeviceFeature>)notification.FeatureSet!;

		var batteryFeature = powerFeatures.GetFeature<IBatteryStateDeviceFeature>();
		var lowPowerModeBatteryThresholdFeature = powerFeatures.GetFeature<ILowPowerModeBatteryThresholdFeature>();
		var idleSleepTimerFeature = powerFeatures.GetFeature<IIdleSleepTimerFeature>();

		bool shouldPersistInformation = false;
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			deviceState = new(this, _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id), notification.DeviceInformation.Id, default);
			_deviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
			shouldPersistInformation = true;
		}

		shouldPersistInformation |= deviceState.OnConnected(batteryFeature, lowPowerModeBatteryThresholdFeature, idleSleepTimerFeature);

		if (shouldPersistInformation)
		{
			await deviceState.PersistInformationAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private void HandleRemoval(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState)) return;

		deviceState.OnDisconnected();
	}

	private void NotifyDeviceConnection(PowerDeviceInformation information) => _deviceChangeListeners.TryWrite(information);

	private void NotifyBatteryStateChange(ChangeWatchNotification<Guid, BatteryState> notification)
	{
		_batteryChangeListeners.TryWrite(notification);
		ProcessBatteryChangeNotification(notification);
	}

	private void NotifyLowPowerBatteryThreshold(PowerDeviceLowPowerBatteryThresholdNotification notification)
		=> _lowPowerBatteryThresholdListeners.TryWrite(notification);

	private void NotifyIdleSleepTimer(PowerDeviceIdleSleepTimerNotification notification)
		=> _idleTimerListeners.TryWrite(notification);

	// In this part of the code, we map device arrivals and status updates to sensible events in a way that hopefully leaves enough information for the handlers to make useful decisions.
	// This means that some of the filtering logic on what to display has to be done on the event handler side.
	// Usages that we want to guarantee here is that of notifications and also an accurate display of the battery level is the user wishes to display it somewhere.
	// Generally, we want to avoid flooding the user with too many notifications, so for those, the handlers will have to compare battery levels and act upon this.
	private void ProcessBatteryChangeNotification(ChangeWatchNotification<Guid, BatteryState> notification)
	{
		switch (notification.NotificationKind)
		{
		case WatchNotificationKind.Enumeration:
		case WatchNotificationKind.Addition:
			_eventWriter.TryWrite
			(
				Event.Create
				(
					BatteryDeviceConnectedEventGuid,
					new BatteryEventParameters
					(
						(DeviceId)notification.Key,
						notification.NewValue.Level,
						notification.OldValue.Level,
						notification.NewValue.BatteryStatus,
						notification.NewValue.ExternalPowerStatus
					)
				)
			);
			break;
		case WatchNotificationKind.Update:
			// Detects if the external power status connection state has changed.
			if (((notification.NewValue.ExternalPowerStatus ^ notification.OldValue.ExternalPowerStatus) & ExternalPowerStatus.IsConnected) != 0)
			{
				var eventGuid = (notification.NewValue.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0 ?
					BatteryExternalPowerConnectedEventGuid :
					BatteryExternalPowerDisconnectedEventGuid;
				_eventWriter.TryWrite
				(
					Event.Create
					(
						eventGuid,
						new BatteryEventParameters
						(
							(DeviceId)notification.Key,
							notification.NewValue.Level,
							notification.OldValue.Level,
							notification.NewValue.BatteryStatus,
							notification.NewValue.ExternalPowerStatus
						)
					)
				);
			}
			else if (notification.NewValue.BatteryStatus != notification.OldValue.BatteryStatus)
			{
				switch (notification.NewValue.BatteryStatus)
				{
				case BatteryStatus.ChargingComplete:
					_eventWriter.TryWrite
					(
						Event.Create
						(
							BatteryChargingCompleteEventGuid,
							new BatteryEventParameters
							(
								(DeviceId)notification.Key,
								1f,
								notification.OldValue.Level,
								notification.NewValue.BatteryStatus,
								notification.NewValue.ExternalPowerStatus
							)
						)
					);
					break;
				case BatteryStatus.Error:
				case BatteryStatus.TooHot:
				case BatteryStatus.Missing:
				case BatteryStatus.Invalid:
					_eventWriter.TryWrite
					(
						Event.Create
						(
							BatteryErrorEventGuid,
							new BatteryEventParameters
							(
								(DeviceId)notification.Key,
								notification.NewValue.Level,
								notification.OldValue.Level,
								notification.NewValue.BatteryStatus,
								notification.NewValue.ExternalPowerStatus
							)
						)
					);
					break;
				}
			}
			else if (notification.NewValue.BatteryStatus == BatteryStatus.Charging)
			{
				_eventWriter.TryWrite
				(
					Event.Create
					(
						BatteryChargingEventGuid,
						new BatteryEventParameters
						(
							(DeviceId)notification.Key,
							notification.NewValue.Level,
							notification.OldValue.Level,
							notification.NewValue.BatteryStatus,
							notification.NewValue.ExternalPowerStatus
						)
					)
				);
			}
			else if (notification.NewValue.BatteryStatus == BatteryStatus.Discharging)
			{
				_eventWriter.TryWrite
				(
					Event.Create
					(
						BatteryDischargingEventGuid,
						new BatteryEventParameters
						(
							(DeviceId)notification.Key,
							notification.NewValue.Level,
							notification.OldValue.Level,
							notification.NewValue.BatteryStatus,
							notification.NewValue.ExternalPowerStatus
						)
					)
				);
			}
			break;
		}
	}

	public async IAsyncEnumerable<PowerDeviceInformation> WatchPowerDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<PowerDeviceInformation>();

		List<PowerDeviceInformation>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}

		List<PowerDeviceInformation>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<PowerDeviceInformation>();
			foreach (var kvp in _deviceStates)
			{
				initialNotifications.Add(kvp.Value.CreatePowerDeviceInformation());
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<ChangeWatchNotification<Guid, BatteryState>> WatchBatteryChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<ChangeWatchNotification<Guid, BatteryState>>();

		List<ChangeWatchNotification<Guid, BatteryState>>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _batteryChangeListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _batteryChangeListeners, channel);
		}

		List<ChangeWatchNotification<Guid, BatteryState>>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<ChangeWatchNotification<Guid, BatteryState>>();
			foreach (var deviceState in _deviceStates.Values)
			{
				if (!deviceState.TryGetBatteryState(out var batteryState)) continue;

				initialNotifications.Add
				(
					new
					(
						WatchNotificationKind.Enumeration,
						deviceState.DeviceId,
						batteryState,
						default
					)
				);
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<PowerDeviceLowPowerBatteryThresholdNotification> WatchLowPowerBatteryThresholdChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<PowerDeviceLowPowerBatteryThresholdNotification>();

		List<PowerDeviceLowPowerBatteryThresholdNotification>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _lowPowerBatteryThresholdListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _lowPowerBatteryThresholdListeners, channel);
		}

		List<PowerDeviceLowPowerBatteryThresholdNotification>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<PowerDeviceLowPowerBatteryThresholdNotification>();
			foreach (var deviceState in _deviceStates.Values)
			{
				if (!deviceState.TryGetLowPowerBatteryThreshold(out var batteryThreshold)) continue;

				initialNotifications.Add(new() { DeviceId = deviceState.DeviceId, BatteryThreshold = batteryThreshold });
			}

			return initialNotifications;
		}
	}

	public async IAsyncEnumerable<PowerDeviceIdleSleepTimerNotification> WatchIdleSleepTimerChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<PowerDeviceIdleSleepTimerNotification>();

		List<PowerDeviceIdleSleepTimerNotification>? initialNotifications;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialNotifications = GetInitialNotifications();
			ArrayExtensions.InterlockedAdd(ref _idleTimerListeners, channel);
		}

		try
		{
			if (initialNotifications is not null)
			{
				for (int i = 0; i < initialNotifications.Count; i++)
				{
					yield return initialNotifications[i];
				}
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _idleTimerListeners, channel);
		}

		List<PowerDeviceIdleSleepTimerNotification>? GetInitialNotifications()
		{
			if (_deviceStates.IsEmpty) return null;

			var initialNotifications = new List<PowerDeviceIdleSleepTimerNotification>();
			foreach (var deviceState in _deviceStates.Values)
			{
				if (!deviceState.TryGetIdleTime(out var idleTime)) continue;

				initialNotifications.Add(new() { DeviceId = deviceState.DeviceId, IdleTime = idleTime });
			}

			return initialNotifications;
		}
	}

	public async Task SetLowPowerModeBatteryThresholdAsync(Guid deviceId, Half threshold, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				await deviceState.SetLowPowerModeBatteryThresholdAsync(threshold, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	public async Task SetIdleSleepTimerAsync(Guid deviceId, TimeSpan idleTimer, CancellationToken cancellationToken)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				await deviceState.SetIdleSleepTimerAsync(idleTimer, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
