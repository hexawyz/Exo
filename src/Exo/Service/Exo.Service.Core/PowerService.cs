using System.Collections.Concurrent;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Features.PowerManagement;
using Exo.Primitives;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Configuration;
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
internal sealed class PowerService :
	IChangeSource<PowerDeviceInformation>,
	IChangeSource<ChangeWatchNotification<Guid, BatteryState>>,
	IChangeSource<PowerDeviceLowPowerBatteryThresholdNotification>,
	IChangeSource<PowerDeviceIdleSleepTimerNotification>,
	IChangeSource<PowerDeviceWirelessBrightnessNotification>,
	IAsyncDisposable
{

	private sealed class DeviceState
	{
		private readonly Lock _lock;
		private readonly PowerService _powerService;
		private readonly IConfigurationContainer _configurationContainer;
		private IBatteryStateDeviceFeature? _batteryFeatures;
		private ILowPowerModeBatteryThresholdFeature? _lowPowerModeBatteryThresholdFeature;
		private IIdleSleepTimerFeature? _idleSleepTimerFeature;
		private IWirelessBrightnessFeature? _wirelessBrightnessFeature;
		private readonly Guid _deviceId;
		private PowerDeviceFlags _flags;
		private TimeSpan _minimumIdleTime;
		private TimeSpan _maximumIdleTime;
		private BatteryState _batteryState;
		private TimeSpan _idleTime;
		private Half _lowPowerBatteryThreshold;
		private byte _minimumBrightness;
		private byte _maximumBrightness;
		private byte _wirelessBrightness;

		public DeviceState(PowerService powerService, IConfigurationContainer configurationContainer, Guid deviceId, PersistedPowerDeviceInformation information)
		{
			_lock = new();
			_powerService = powerService;
			_configurationContainer = configurationContainer;
			_deviceId = deviceId;
			_flags = information.Capabilities;
			_minimumIdleTime = information.MinimumIdleTime;
			_maximumIdleTime = information.MaximumIdleTime;
		}

		public Guid DeviceId => _deviceId;
		public bool IsConnected => (_flags & PowerDeviceFlags.IsConnected) != 0;
		public bool HasBattery => (_flags & PowerDeviceFlags.HasBattery) != 0;
		public bool HasLowPowerBatteryThreshold => (_flags & PowerDeviceFlags.HasLowPowerBatteryThreshold) != 0;
		public bool HasIdleTimer => (_flags & PowerDeviceFlags.HasIdleTimer) != 0;
		public bool HasWirelessBrightness => (_flags & PowerDeviceFlags.HasWirelessBrightness) != 0;

		public bool OnConnected
		(
			IBatteryStateDeviceFeature? batteryFeatures,
			ILowPowerModeBatteryThresholdFeature? lowPowerModeBatteryThresholdFeature,
			IIdleSleepTimerFeature? idleSleepTimerFeature,
			IWirelessBrightnessFeature? wirelessBrightnessFeature
		)
		{
			bool hasChanged = false;
			lock (_lock)
			{
				if (IsConnected) throw new InvalidOperationException();

				PowerDeviceFlags capabilities = PowerDeviceFlags.IsConnected;

				if (batteryFeatures is not null) capabilities |= PowerDeviceFlags.HasBattery;
				if (lowPowerModeBatteryThresholdFeature is not null) capabilities |= PowerDeviceFlags.HasLowPowerBatteryThreshold;

				if (idleSleepTimerFeature is not null)
				{
					capabilities |= PowerDeviceFlags.HasIdleTimer;
					hasChanged |= _minimumIdleTime != idleSleepTimerFeature.MinimumIdleTime;
					_minimumIdleTime = idleSleepTimerFeature.MinimumIdleTime;
					hasChanged |= _maximumIdleTime != idleSleepTimerFeature.MaximumIdleTime;
					_maximumIdleTime = idleSleepTimerFeature.MaximumIdleTime;
				}

				if (wirelessBrightnessFeature is not null)
				{
					capabilities |= PowerDeviceFlags.HasWirelessBrightness;
					hasChanged |= _minimumBrightness != wirelessBrightnessFeature.MinimumValue;
					_minimumBrightness = wirelessBrightnessFeature.MinimumValue;
					hasChanged |= _maximumBrightness != wirelessBrightnessFeature.MaximumValue;
					_maximumBrightness = wirelessBrightnessFeature.MaximumValue;
				}

				hasChanged |= (capabilities & ~PowerDeviceFlags.IsConnected) != (_flags & ~PowerDeviceFlags.IsConnected);

				_flags = capabilities;

				_batteryFeatures = batteryFeatures;
				_lowPowerModeBatteryThresholdFeature = lowPowerModeBatteryThresholdFeature;
				_idleSleepTimerFeature = idleSleepTimerFeature;
				_wirelessBrightnessFeature = wirelessBrightnessFeature;

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
					_powerService.NotifyLowPowerBatteryThreshold(new() { DeviceId = DeviceId, BatteryThreshold = _lowPowerBatteryThreshold });
				}

				if (idleSleepTimerFeature is not null)
				{
					_idleTime = idleSleepTimerFeature.IdleTime;
					_powerService.NotifyIdleSleepTimer(new() { DeviceId = DeviceId, IdleTime = _idleTime });
				}

				if (wirelessBrightnessFeature is not null)
				{
					_wirelessBrightness = wirelessBrightnessFeature.WirelessBrightness;
					_powerService.NotifyWirelessBrightness(new() { DeviceId = DeviceId, Brightness = _wirelessBrightness });
				}
			}
			return hasChanged;
		}

		public void OnDisconnected()
		{
			lock (_lock)
			{
				if (!IsConnected) throw new InvalidOperationException();

				if (_batteryFeatures is not null) _batteryFeatures.BatteryStateChanged -= OnBatteryStateChanged;

				_flags &= ~PowerDeviceFlags.IsConnected;

				_batteryFeatures = null;
				_lowPowerModeBatteryThresholdFeature = null;
				_idleSleepTimerFeature = null;
				_wirelessBrightnessFeature = null;

				_powerService.NotifyDeviceConnection(CreatePowerDeviceInformation());
			}
		}

		public async Task PersistInformationAsync(CancellationToken cancellationToken)
			=> await _configurationContainer.WriteValueAsync
			(
				new PersistedPowerDeviceInformation()
				{
					Capabilities = _flags & ~PowerDeviceFlags.IsConnected,
					MinimumIdleTime = _minimumIdleTime,
					MaximumIdleTime = _maximumIdleTime
				},
				SourceGenerationContext.Default.PersistedPowerDeviceInformation,
				cancellationToken
			).ConfigureAwait(false);

		public PowerDeviceInformation CreatePowerDeviceInformation()
			=> new
			(
				_deviceId,
				_flags,
				_minimumIdleTime,
				_maximumIdleTime,
				_minimumBrightness,
				_maximumBrightness
			);

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

		public bool TryGetWirelessBrightness(out byte wirelessBrightness)
		{
			if (HasWirelessBrightness && IsConnected)
			{
				wirelessBrightness = _wirelessBrightness;
				return true;
			}
			wirelessBrightness = default;
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

		public async Task SetWirelessBrightnessAsync(byte brightness, CancellationToken cancellationToken)
		{
			if (_wirelessBrightnessFeature is { } wirelessBrightnessFeature)
			{
				await wirelessBrightnessFeature.SetWirelessBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
				_wirelessBrightness = brightness;
				_powerService.NotifyWirelessBrightness(new() { DeviceId = DeviceId, Brightness = brightness });
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

			var result = await deviceConfigurationContainer.ReadValueAsync(SourceGenerationContext.Default.PersistedPowerDeviceInformation, cancellationToken).ConfigureAwait(false);

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
	private ChangeBroadcaster<PowerDeviceInformation> _deviceChangeBroadcaster;
	private ChangeBroadcaster<ChangeWatchNotification<Guid, BatteryState>> _batteryChangeBroadcaster;
	private ChangeBroadcaster<PowerDeviceLowPowerBatteryThresholdNotification> _lowPowerBatteryThresholdBroadcaster;
	private ChangeBroadcaster<PowerDeviceIdleSleepTimerNotification> _idleTimerBroadcaster;
	private ChangeBroadcaster<PowerDeviceWirelessBrightnessNotification> _wirelessBrightnessBroadcaster;
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
		var wirelessBrightnessFeature = powerFeatures.GetFeature<IWirelessBrightnessFeature>();

		bool shouldPersistInformation = false;
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			deviceState = new(this, _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id), notification.DeviceInformation.Id, default);
			_deviceStates.TryAdd(notification.DeviceInformation.Id, deviceState);
			shouldPersistInformation = true;
		}

		shouldPersistInformation |= deviceState.OnConnected(batteryFeature, lowPowerModeBatteryThresholdFeature, idleSleepTimerFeature, wirelessBrightnessFeature);

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

	private void NotifyDeviceConnection(PowerDeviceInformation information) => _deviceChangeBroadcaster.Push(information);

	private void NotifyBatteryStateChange(ChangeWatchNotification<Guid, BatteryState> notification)
	{
		_batteryChangeBroadcaster.Push(notification);
		ProcessBatteryChangeNotification(notification);
	}

	private void NotifyLowPowerBatteryThreshold(PowerDeviceLowPowerBatteryThresholdNotification notification)
		=> _lowPowerBatteryThresholdBroadcaster.Push(notification);

	private void NotifyIdleSleepTimer(PowerDeviceIdleSleepTimerNotification notification)
		=> _idleTimerBroadcaster.Push(notification);

	private void NotifyWirelessBrightness(PowerDeviceWirelessBrightnessNotification notification)
		=> _wirelessBrightnessBroadcaster.Push(notification);

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

	async ValueTask<PowerDeviceInformation[]?> IChangeSource<PowerDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<PowerDeviceInformation> writer, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!_deviceStates.IsEmpty)
			{
				var initialNotifications = new List<PowerDeviceInformation>();
				foreach (var kvp in _deviceStates)
				{
					initialNotifications.Add(kvp.Value.CreatePowerDeviceInformation());
				}

				_deviceChangeBroadcaster.Register(writer);
				return [.. initialNotifications];
			}
			else
			{
				_deviceChangeBroadcaster.Register(writer);
				return null;
			}
		}
	}

	void IChangeSource<PowerDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<PowerDeviceInformation> writer)
		=> _deviceChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<PowerDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<PowerDeviceInformation> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_deviceChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<ChangeWatchNotification<Guid, BatteryState>[]?> IChangeSource<ChangeWatchNotification<Guid, BatteryState>>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<ChangeWatchNotification<Guid, BatteryState>> writer, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!_deviceStates.IsEmpty)
			{
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
				_batteryChangeBroadcaster.Register(writer);
				return [.. initialNotifications];
			}
			else
			{
				_batteryChangeBroadcaster.Register(writer);
				return null;
			}
		}
	}

	void IChangeSource<ChangeWatchNotification<Guid, BatteryState>>.UnsafeUnregisterWatcher(ChannelWriter<ChangeWatchNotification<Guid, BatteryState>> writer)
		=> _batteryChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<ChangeWatchNotification<Guid, BatteryState>>.SafeUnregisterWatcherAsync(ChannelWriter<ChangeWatchNotification<Guid, BatteryState>> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_batteryChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<PowerDeviceLowPowerBatteryThresholdNotification[]?> IChangeSource<PowerDeviceLowPowerBatteryThresholdNotification>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<PowerDeviceLowPowerBatteryThresholdNotification> writer, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!_deviceStates.IsEmpty)
			{
				var initialNotifications = new List<PowerDeviceLowPowerBatteryThresholdNotification>();
				foreach (var deviceState in _deviceStates.Values)
				{
					if (!deviceState.TryGetLowPowerBatteryThreshold(out var batteryThreshold)) continue;

					initialNotifications.Add(new() { DeviceId = deviceState.DeviceId, BatteryThreshold = batteryThreshold });
				}
				_lowPowerBatteryThresholdBroadcaster.Register(writer);
				return [.. initialNotifications];
			}
			else
			{
				_lowPowerBatteryThresholdBroadcaster.Register(writer);
				return null;
			}
		}
	}

	void IChangeSource<PowerDeviceLowPowerBatteryThresholdNotification>.UnsafeUnregisterWatcher(ChannelWriter<PowerDeviceLowPowerBatteryThresholdNotification> writer)
		=> _lowPowerBatteryThresholdBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<PowerDeviceLowPowerBatteryThresholdNotification>.SafeUnregisterWatcherAsync(ChannelWriter<PowerDeviceLowPowerBatteryThresholdNotification> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_lowPowerBatteryThresholdBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<PowerDeviceIdleSleepTimerNotification[]?> IChangeSource<PowerDeviceIdleSleepTimerNotification>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<PowerDeviceIdleSleepTimerNotification> writer, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!_deviceStates.IsEmpty)
			{
				var initialNotifications = new List<PowerDeviceIdleSleepTimerNotification>();
				foreach (var deviceState in _deviceStates.Values)
				{
					if (!deviceState.TryGetIdleTime(out var idleTime)) continue;

					initialNotifications.Add(new() { DeviceId = deviceState.DeviceId, IdleTime = idleTime });
				}
				_idleTimerBroadcaster.Register(writer);
				return [.. initialNotifications];
			}
			else
			{
				_idleTimerBroadcaster.Register(writer);
				return null;
			}
		}
	}

	void IChangeSource<PowerDeviceIdleSleepTimerNotification>.UnsafeUnregisterWatcher(ChannelWriter<PowerDeviceIdleSleepTimerNotification> writer)
		=> _idleTimerBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<PowerDeviceIdleSleepTimerNotification>.SafeUnregisterWatcherAsync(ChannelWriter<PowerDeviceIdleSleepTimerNotification> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_idleTimerBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<PowerDeviceWirelessBrightnessNotification[]?> IChangeSource<PowerDeviceWirelessBrightnessNotification>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<PowerDeviceWirelessBrightnessNotification> writer, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!_deviceStates.IsEmpty)
			{
				var initialNotifications = new List<PowerDeviceWirelessBrightnessNotification>();
				foreach (var deviceState in _deviceStates.Values)
				{
					if (!deviceState.TryGetWirelessBrightness(out var brightness)) continue;

					initialNotifications.Add(new() { DeviceId = deviceState.DeviceId, Brightness = brightness });
				}
				_wirelessBrightnessBroadcaster.Register(writer);
				return [.. initialNotifications];
			}
			else
			{
				_wirelessBrightnessBroadcaster.Register(writer);
				return null;
			}
		}
	}

	void IChangeSource<PowerDeviceWirelessBrightnessNotification>.UnsafeUnregisterWatcher(ChannelWriter<PowerDeviceWirelessBrightnessNotification> writer)
		=> _wirelessBrightnessBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<PowerDeviceWirelessBrightnessNotification>.SafeUnregisterWatcherAsync(ChannelWriter<PowerDeviceWirelessBrightnessNotification> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_wirelessBrightnessBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	public async Task SetLowPowerModeBatteryThresholdAsync(Guid deviceId, Half threshold, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			await deviceState.SetLowPowerModeBatteryThresholdAsync(threshold, cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task SetIdleSleepTimerAsync(Guid deviceId, TimeSpan idleTimer, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			await deviceState.SetIdleSleepTimerAsync(idleTimer, cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task SetWirelessBrightnessAsync(Guid deviceId, byte brightness, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			await deviceState.SetWirelessBrightnessAsync(brightness, cancellationToken).ConfigureAwait(false);
		}
	}
}
