using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using System.Windows.Input;
using Exo.Contracts.Ui.Settings;
using Exo.Service;
using Exo.Settings.Ui.Converters;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable, IConnectedState
{
	private static readonly UnboundedChannelOptions DeviceChannelOptions = new() { SingleWriter = true, SingleReader = true, AllowSynchronousContinuations = true };

	private static class Commands
	{
		public class NavigateToDeviceCommand : ICommand
		{
			private readonly ICommand _rootNavigationCommand;

			public NavigateToDeviceCommand(ICommand rootNavigationCommand) => _rootNavigationCommand = rootNavigationCommand;

			public void Execute(object? parameter)
			{
				ArgumentNullException.ThrowIfNull(parameter);
				if (parameter is DeviceViewModel device)
				{
					_rootNavigationCommand.Execute(new PageViewModel("Device", device.FriendlyName, DeviceCategoryToGlyphConverter.GetGlyph(device.Category), device.Id));
				}
			}

			public bool CanExecute(object? parameter) => parameter is DeviceViewModel;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}
	}

	private readonly ObservableCollection<DeviceViewModel> _devices;

	// All removed device IDs are definitely removed. Device removal is a manual request from the user, and can only happen when the device is disconnected.
	// If the same device is reconnected later, it will be considered a new device and get a new and device ID.
	private readonly HashSet<Guid> _removedDeviceIds;

	private readonly SettingsServiceConnectionManager _connectionManager;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _availableImages;

	// Processing asynchronous status updates requires accessing the view model from the device ID.
	private readonly Dictionary<Guid, DeviceViewModel> _devicesById;

	// Various storages for pending changes for when the device view model is not available at reception.
	private readonly Dictionary<Guid, PowerDeviceInformation> _pendingPowerDeviceInformations;
	private readonly Dictionary<Guid, BatteryStateViewModel> _pendingBatteryChanges;
	private readonly Dictionary<Guid, TimeSpan> _pendingIdleSleepTimerChanges;
	private readonly Dictionary<Guid, byte> _pendingWirelessBrightnessChanges;
	private readonly Dictionary<Guid, Half> _pendingLowPowerModeBatteryThresholdChanges;
	private readonly Dictionary<Guid, MouseDeviceInformation> _pendingMouseInformations;
	private readonly Dictionary<Guid, ImmutableArray<DotsPerInch>> _pendingDpiPresetChanges;
	private readonly Dictionary<Guid, (byte? ActivePresetIndex, DotsPerInch Dpi)> _pendingMouseDpiChanges;
	private readonly Dictionary<Guid, ushort> _pendingPollingFrequencyChanges;
	private readonly Dictionary<Guid, MonitorInformation> _pendingMonitorInformations;
	private readonly Dictionary<Guid, List<MonitorSettingValue>> _pendingMonitorSettingChanges;
	private readonly Dictionary<Guid, EmbeddedMonitorDeviceInformation> _pendingEmbeddedMonitorDeviceInformations;
	private readonly Dictionary<Guid, List<EmbeddedMonitorConfigurationUpdate>> _pendingEmbeddedMonitorConfigurationChanges;
	private readonly Dictionary<Guid, LightDeviceInformation> _pendingLightDeviceInformations;
	private readonly Dictionary<Guid, List<LightChangeNotification>> _pendingLightChanges;

	// Temporary channel that is necessary until dependent services are moved to the new protocol.
	private Channel<(WatchNotificationKind, DeviceStateInformation)> _deviceArrivalChannel;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice;

	private readonly ISettingsMetadataService _metadataService;
	private IPowerService? _powerService;
	private IMouseService? _mouseService;
	private IMonitorService? _monitorService;
	private readonly IRasterizationScaleProvider _rasterizationScaleProvider;
	private readonly INotificationSystem _notificationSystem;

	private readonly Commands.NavigateToDeviceCommand _navigateToDeviceCommand;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public DevicesViewModel
	(
		SettingsServiceConnectionManager connectionManager,
		ReadOnlyObservableCollection<ImageViewModel> availableImages,
		ISettingsMetadataService metadataService,
		IRasterizationScaleProvider rasterizationScaleProvider,
		INotificationSystem notificationSystem,
		ICommand navigateCommand
	)
	{
		_devices = new();
		_removedDeviceIds = new();
		_connectionManager = connectionManager;
		_availableImages = availableImages;
		_deviceArrivalChannel = Channel.CreateUnbounded<(WatchNotificationKind, DeviceStateInformation)>(DeviceChannelOptions);
		_devicesById = new();
		_pendingPowerDeviceInformations = new();
		_pendingBatteryChanges = new();
		_pendingIdleSleepTimerChanges = new();
		_pendingWirelessBrightnessChanges = new();
		_pendingLowPowerModeBatteryThresholdChanges = new();
		_pendingMouseInformations = new();
		_pendingMouseDpiChanges = new();
		_pendingDpiPresetChanges = new();
		_pendingPollingFrequencyChanges = new();
		_pendingMonitorInformations = new();
		_pendingMonitorSettingChanges = new();
		_pendingEmbeddedMonitorDeviceInformations = new();
		_pendingEmbeddedMonitorConfigurationChanges = new();
		_pendingLightDeviceInformations = new();
		_pendingLightChanges = new();
		_metadataService = metadataService;
		_rasterizationScaleProvider = rasterizationScaleProvider;
		_notificationSystem = notificationSystem;
		_navigateToDeviceCommand = new(navigateCommand);
		_cancellationTokenSource = new CancellationTokenSource();
		_stateRegistration = _connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		_stateRegistration.Dispose();
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	public ICommand NavigateToDeviceCommand => _navigateToDeviceCommand;

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			var embeddedMonitorService = await _connectionManager.GetEmbeddedMonitorServiceAsync(cancellationToken);
			var lightService = await _connectionManager.GetLightServiceAsync(cancellationToken);

			var deviceWatchTask = WatchDevicesAsync(_deviceArrivalChannel.Reader, embeddedMonitorService, lightService, cts.Token);

			var embeddedMonitorDeviceWatchTask = WatchEmbeddedMonitorDevicesAsync(embeddedMonitorService, cts.Token);
			var embeddedMonitorConfigurationWatchTask = WatchEmbeddedMonitorConfigurationChangesAsync(embeddedMonitorService, cts.Token);

			var lightDeviceWatchTask = WatchLightDevicesAsync(lightService, cts.Token);
			var lightChangeWatchTask = WatchLightChangesAsync(lightService, cts.Token);

			try
			{
				await Task.WhenAll
				(
					[
						deviceWatchTask,
						embeddedMonitorDeviceWatchTask,
						embeddedMonitorConfigurationWatchTask,
						lightDeviceWatchTask,
						lightChangeWatchTask,
					]
				);
			}
			catch (Exception)
			{
			}
		}
	}

	void IConnectedState.Reset()
	{
		_removedDeviceIds.Clear();
		_devicesById.Clear();

		_pendingEmbeddedMonitorDeviceInformations.Clear();
		_pendingEmbeddedMonitorConfigurationChanges.Clear();

		_pendingLightDeviceInformations.Clear();
		_pendingLightChanges.Clear();

		SelectedDevice = null;

		foreach (var device in _devices)
		{
			device.IsAvailable = false;
		}

		_devices.Clear();
	}

	internal void HandleDeviceNotification(WatchNotificationKind kind, DeviceStateInformation information)
	{
		// TODO: Once the dependent services are migrated, move the code from WatchDevicesAsync in there.
		_deviceArrivalChannel.Writer.TryWrite((kind, information));
	}

	internal void OnConnected(IPowerService powerService, IMouseService mouseService, IMonitorService monitorService)
	{
		_powerService = powerService;
		_mouseService = mouseService;
		_monitorService = monitorService;
	}

	internal void Reset()
	{
		_deviceArrivalChannel.Writer.TryComplete();
		_deviceArrivalChannel = Channel.CreateUnbounded<(WatchNotificationKind, DeviceStateInformation)>(DeviceChannelOptions);

		_pendingPowerDeviceInformations.Clear();
		_pendingBatteryChanges.Clear();
		_pendingIdleSleepTimerChanges.Clear();
		_pendingWirelessBrightnessChanges.Clear();
		_pendingLowPowerModeBatteryThresholdChanges.Clear();

		_pendingMouseInformations.Clear();
		_pendingMouseDpiChanges.Clear();
		_pendingDpiPresetChanges.Clear();
		_pendingPollingFrequencyChanges.Clear();

		_pendingMonitorInformations.Clear();
		_pendingMonitorSettingChanges.Clear();

		_powerService = null;
		_monitorService = null;
	}

	private async Task WatchDevicesAsync
	(
		ChannelReader<(WatchNotificationKind, DeviceStateInformation)> reader,
		IEmbeddedMonitorService embeddedMonitorService,
		ILightService lightService,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await foreach (var (kind, information) in reader.ReadAllAsync(cancellationToken))
			{
				switch (kind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					{
						var device = new DeviceViewModel
						(
							_connectionManager,
							_availableImages,
							_metadataService,
							_notificationSystem,
							_powerService!,
							_mouseService!,
							_monitorService!,
							embeddedMonitorService,
							lightService,
							_rasterizationScaleProvider,
							information
						);
						await HandleDeviceArrivalAsync(device, cancellationToken);
						_devicesById.Add(information.Id, device);
						_devices.Add(device);
					}
					break;
				case WatchNotificationKind.Removal:
					_removedDeviceIds.Add(information.Id);
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == information.Id)
						{
							_devices.RemoveAt(i);
							_devicesById.Remove(information.Id);
							HandleDeviceRemoval(device);
							break;
						}
					}
					break;
				case WatchNotificationKind.Update:
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == information.Id)
						{
							device.FriendlyName = information.FriendlyName;
							device.Category = information.Category;
							if (information.IsAvailable != device.IsAvailable)
							{
								if (device.IsAvailable = information.IsAvailable)
								{
									await HandleDeviceArrivalAsync(device, cancellationToken);
								}
								else
								{
									HandleDeviceRemoval(device);
								}
							}
							device.UpdateDeviceIds(information.DeviceIds, information.MainDeviceIdIndex);
							device.SerialNumber = information.SerialNumber;
							break;
						}
					}
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch
		{
		}
	}

	private async Task HandleDeviceArrivalAsync(DeviceViewModel device, CancellationToken cancellationToken)
	{
		if (device.PowerFeatures is { } powerFeatures)
		{
			if (_pendingPowerDeviceInformations.Remove(device.Id, out var powerDeviceInformation))
			{
				powerFeatures.UpdateInformation(powerDeviceInformation);
			}
			if (_pendingBatteryChanges.Remove(device.Id, out var batteryStatus))
			{
				powerFeatures.BatteryState = batteryStatus;
			}
			if (_pendingLowPowerModeBatteryThresholdChanges.Remove(device.Id, out var batteryThreshold))
			{
				powerFeatures.UpdateLowPowerModeBatteryThreshold(batteryThreshold);
			}
			if (_pendingIdleSleepTimerChanges.Remove(device.Id, out var idleSleepDelay))
			{
				powerFeatures.UpdateIdleSleepTimer(idleSleepDelay);
			}
			if (_pendingWirelessBrightnessChanges.Remove(device.Id, out var brightness))
			{
				powerFeatures.UpdateWirelessBrightness(brightness);
			}
		}
		if (device.MouseFeatures is { } mouseFeatures)
		{
			if (_pendingMouseInformations.Remove(device.Id, out var mouseInformation))
			{
				mouseFeatures.UpdateInformation(mouseInformation);
			}
			if (_pendingDpiPresetChanges.Remove(device.Id, out var mouseDpiPresets))
			{
				mouseFeatures.UpdatePresets(mouseDpiPresets);
			}
			if (_pendingMouseDpiChanges.Remove(device.Id, out var dpiUpdate))
			{
				mouseFeatures.UpdateCurrentDpi(dpiUpdate.ActivePresetIndex, dpiUpdate.Dpi);
			}
			if (_pendingPollingFrequencyChanges.Remove(device.Id, out var pollingFrequency))
			{
				mouseFeatures.UpdateCurrentPollingFrequency(pollingFrequency);
			}
		}
		if (device.MonitorFeatures is { } monitorFeatures)
		{
			if (_pendingMonitorInformations.Remove(device.Id, out var monitorInformation))
			{
				await monitorFeatures.UpdateInformationAsync(monitorInformation, cancellationToken);
			}
			if (_pendingMonitorSettingChanges.Remove(device.Id, out var monitorSettings))
			{
				foreach (var setting in monitorSettings)
				{
					monitorFeatures.UpdateSetting(setting);
				}
			}
		}
		if (device.EmbeddedMonitorFeatures is { } embeddedMonitorFeatures)
		{
			if (_pendingEmbeddedMonitorDeviceInformations.Remove(device.Id, out var embeddedMonitorDeviceInformation))
			{
				embeddedMonitorFeatures.UpdateInformation(embeddedMonitorDeviceInformation);
			}
			if (_pendingEmbeddedMonitorConfigurationChanges.Remove(device.Id, out var embeddedMonitorConfigurations))
			{
				foreach (var embeddedMonitorConfiguration in embeddedMonitorConfigurations)
				{
					embeddedMonitorFeatures.UpdateConfiguration(embeddedMonitorConfiguration);
				}
			}
		}
		if (device.LightFeatures is { } lightFeatures)
		{
			if (_pendingLightDeviceInformations.Remove(device.Id, out var lightInformation))
			{
				lightFeatures.UpdateInformation(lightInformation);
			}
			if (_pendingLightChanges.Remove(device.Id, out var lightChanges))
			{
				foreach (var lightChange in lightChanges)
				{
					lightFeatures.UpdateLightState(lightChange);
				}
			}
		}
	}

	private void HandleDeviceRemoval(DeviceViewModel device)
	{
		if (device.PowerFeatures is { } powerFeatures)
		{
			powerFeatures.BatteryState = null;
		}
		_pendingBatteryChanges.Remove(device.Id, out _);

		_pendingPowerDeviceInformations.Remove(device.Id, out _);
		_pendingBatteryChanges.Remove(device.Id, out _);
		_pendingIdleSleepTimerChanges.Remove(device.Id, out _);
		_pendingWirelessBrightnessChanges.Remove(device.Id, out _);
		_pendingLowPowerModeBatteryThresholdChanges.Remove(device.Id, out _);

		_pendingMouseInformations.Remove(device.Id, out _);
		_pendingMouseDpiChanges.Remove(device.Id, out _);
		_pendingDpiPresetChanges.Remove(device.Id, out _);
		_pendingPollingFrequencyChanges.Remove(device.Id, out _);

		_pendingMonitorInformations.Remove(device.Id, out _);
		_pendingMonitorSettingChanges.Remove(device.Id, out _);

		_pendingEmbeddedMonitorDeviceInformations.Remove(device.Id, out _);
		_pendingEmbeddedMonitorConfigurationChanges.Remove(device.Id, out _);

		_pendingLightDeviceInformations.Remove(device.Id, out _);
		_pendingLightChanges.Remove(device.Id, out _);
	}

	internal void HandlePowerDeviceUpdate(PowerDeviceInformation powerDevice)
	{
		if (_devicesById.TryGetValue(powerDevice.DeviceId, out var device))
		{
			if (device.PowerFeatures is { } powerFeatures)
			{
				powerFeatures.UpdateInformation(powerDevice);
			}
		}
		else
		{
			_pendingPowerDeviceInformations[powerDevice.DeviceId] = powerDevice;
		}
	}

	internal void HandleBatteryUpdate(BatteryChangeNotification notification)
	{
		var status = new BatteryStateViewModel(notification);
		if (_devicesById.TryGetValue(notification.DeviceId, out var device))
		{
			if (device.PowerFeatures is { } powerFeatures)
			{
				powerFeatures.BatteryState = status;
			}
		}
		else
		{
			_pendingBatteryChanges[notification.DeviceId] = status;
		}
	}

	internal void HandleLowPowerModeBatteryThresholdUpdate(Guid deviceId, Half batteryThreshold)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.PowerFeatures is { } powerFeatures)
			{
				powerFeatures.UpdateLowPowerModeBatteryThreshold(batteryThreshold);
			}
		}
		else
		{
			_pendingLowPowerModeBatteryThresholdChanges[deviceId] = batteryThreshold;
		}
	}

	internal void HandleIdleSleepTimerUpdate(Guid deviceId, TimeSpan idleTime)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.PowerFeatures is { } powerFeatures)
			{
				powerFeatures.UpdateIdleSleepTimer(idleTime);
			}
		}
		else
		{
			_pendingIdleSleepTimerChanges[deviceId] = idleTime;
		}
	}

	internal void HandleWirelessBrightnessUpdate(Guid deviceId, byte brightness)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.PowerFeatures is { } powerFeatures)
			{
				powerFeatures.UpdateWirelessBrightness(brightness);
			}
		}
		else
		{
			_pendingWirelessBrightnessChanges[deviceId] = brightness;
		}
	}

	internal void HandleMouseDeviceUpdate(MouseDeviceInformation mouseDevice)
	{
		if (_devicesById.TryGetValue(mouseDevice.DeviceId, out var device))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.UpdateInformation(mouseDevice);
			}
		}
		else
		{
			_pendingMouseInformations[mouseDevice.DeviceId] = mouseDevice;
		}
	}

	internal void HandleMouseDpiUpdate(Guid deviceId, byte? activeDpiPresetIndex, DotsPerInch dpi)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.UpdateCurrentDpi(activeDpiPresetIndex, dpi);
			}
		}
		else
		{
			_pendingMouseDpiChanges[deviceId] = (activeDpiPresetIndex, dpi);
		}
	}

	internal void HandleMouseDpiPresetsUpdate(Guid deviceId, byte? activeDpiPresetIndex, ImmutableArray<DotsPerInch> dpiPresets)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.UpdatePresets(dpiPresets);
			}
		}
		else
		{
			_pendingDpiPresetChanges[deviceId] = dpiPresets;
		}
	}

	internal void HandleMousePollingFrequencyUpdate(Guid deviceId, ushort pollingFrequency)
	{
		if (_devicesById.TryGetValue(deviceId, out var device))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.UpdateCurrentPollingFrequency(pollingFrequency);
			}
		}
		else
		{
			_pendingPollingFrequencyChanges[deviceId] = pollingFrequency;
		}
	}

	internal async void HandleMonitorDeviceUpdate(MonitorInformation monitorDevice)
	{
		if (_devicesById.TryGetValue(monitorDevice.DeviceId, out var device))
		{
			if (device.MonitorFeatures is { } monitorFeatures)
			{
				await monitorFeatures.UpdateInformationAsync(monitorDevice, _cancellationTokenSource.Token);
			}
		}
		else
		{
			_pendingMonitorInformations[monitorDevice.DeviceId] = monitorDevice;
		}
	}

	internal void HandleMonitorSettingUpdate(MonitorSettingValue setting)
	{
		if (_devicesById.TryGetValue(setting.DeviceId, out var device))
		{
			if (device.MonitorFeatures is { } monitorFeatures)
			{
				monitorFeatures.UpdateSetting(setting);
			}
		}
		else
		{
			if (!_pendingMonitorSettingChanges.TryGetValue(setting.DeviceId, out var changes))
			{
				_pendingMonitorSettingChanges[setting.DeviceId] = changes = [];
			}
			changes.Add(setting);
		}
	}

	private async Task WatchEmbeddedMonitorDevicesAsync(IEmbeddedMonitorService embeddedMonitorService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in embeddedMonitorService.WatchEmbeddedMonitorDevicesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.EmbeddedMonitorFeatures is { } embeddedMonitorFeatures)
					{
						embeddedMonitorFeatures.UpdateInformation(notification);
					}
				}
				else
				{
					_pendingEmbeddedMonitorDeviceInformations[notification.DeviceId] = notification;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchEmbeddedMonitorConfigurationChangesAsync(IEmbeddedMonitorService embeddedMonitorService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in embeddedMonitorService.WatchConfigurationUpdatesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.EmbeddedMonitorFeatures is { } embeddedMonitorFeatures)
					{
						embeddedMonitorFeatures.UpdateConfiguration(notification);
					}
				}
				else
				{
					if (!_pendingEmbeddedMonitorConfigurationChanges.TryGetValue(notification.DeviceId, out var changes))
					{
						_pendingEmbeddedMonitorConfigurationChanges[notification.DeviceId] = changes = [];
					}
					changes.Add(notification);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchLightDevicesAsync(ILightService lightService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in lightService.WatchLightDevicesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.LightFeatures is { } lightFeatures)
					{
						lightFeatures.UpdateInformation(notification);
					}
				}
				else
				{
					_pendingLightDeviceInformations[notification.DeviceId] = notification;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchLightChangesAsync(ILightService lightService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in lightService.WatchLightChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.LightFeatures is { } lightFeatures)
					{
						lightFeatures.UpdateLightState(notification);
					}
				}
				else
				{
					if (!_pendingLightChanges.TryGetValue(notification.DeviceId, out var changes))
					{
						_pendingLightChanges[notification.DeviceId] = changes = [];
					}
					changes.Add(notification);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ObservableCollection<DeviceViewModel> Devices => _devices;

	public DeviceViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set => SetValue(ref _selectedDevice, value);
	}

	// If the ID is marked as removed, it means that it will "never" be used again for a device.
	public bool IsRemovedId(Guid id) => _removedDeviceIds.Contains(id);

	public bool TryGetDevice(Guid id, [NotNullWhen(true)] out DeviceViewModel? device)
		=> _devicesById.TryGetValue(id, out device);
}
