using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Converters;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable, IConnectedState
{
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

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice;

	private readonly ISettingsMetadataService _metadataService;
	private readonly IRasterizationScaleProvider _rasterizationScaleProvider;

	private readonly Commands.NavigateToDeviceCommand _navigateToDeviceCommand;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public DevicesViewModel
	(
		SettingsServiceConnectionManager connectionManager,
		ReadOnlyObservableCollection<ImageViewModel> availableImages,
		ISettingsMetadataService metadataService,
		IRasterizationScaleProvider rasterizationScaleProvider,
		ICommand navigateCommand
	)
	{
		_devices = new();
		_removedDeviceIds = new();
		_connectionManager = connectionManager;
		_availableImages = availableImages;
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
		_metadataService = metadataService;
		_rasterizationScaleProvider = rasterizationScaleProvider;
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
			var deviceService = await _connectionManager.GetDeviceServiceAsync(cancellationToken).ConfigureAwait(false);
			var powerService = await _connectionManager.GetPowerServiceAsync(cancellationToken).ConfigureAwait(false);
			var mouseService = await _connectionManager.GetMouseServiceAsync(cancellationToken).ConfigureAwait(false);
			var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken).ConfigureAwait(false);
			var embeddedMonitorService = await _connectionManager.GetEmbeddedMonitorServiceAsync(cancellationToken).ConfigureAwait(false);

			var deviceWatchTask = WatchDevicesAsync(deviceService, powerService, mouseService, embeddedMonitorService, cts.Token);

			var powerWatchTask = WatchPowerDevicesAsync(powerService, cts.Token);
			var batteryWatchTask = WatchBatteryChangesAsync(powerService, cts.Token);
			var lowPowerModeBatteryThresholdWatchTask = WatchLowPowerModeBatteryThresholdChangesAsync(powerService, cts.Token);
			var idleSleepTimerWatchTask = WatchIdleSleepTimerChangesAsync(powerService, cts.Token);
			var wirelessBrightnessWatchTask = WatchWirelessBrightnessChangesAsync(powerService, cts.Token);

			var mouseWatchTask = WatchMouseDevicesAsync(mouseService, cts.Token);
			var mouseDpiWatchTask = WatchMouseDpiChangesAsync(mouseService, cts.Token);
			var mouseDpiPresetWatchTask = WatchMouseDpiPresetsAsync(mouseService, cts.Token);
			var mouseDpiPollingFrequencyWatchTask = WatchMousePollingFrequencyChangesAsync(mouseService, cts.Token);

			var monitorWatchTask = WatchMonitorsAsync(monitorService, cts.Token);
			var monitorSettingWatchTask = WatchMonitorSettingChangesAsync(monitorService, cts.Token);

			var embeddedMonitorDeviceWatchTask = WatchEmbeddedMonitorDevicesAsync(embeddedMonitorService, cts.Token);
			var embeddedMonitorConfigurationWatchTask = WatchEmbeddedMonitorConfigurationChangesAsync(embeddedMonitorService, cts.Token);

			try
			{
				await Task.WhenAll
				(
					[
						deviceWatchTask,
						powerWatchTask,
						batteryWatchTask,
						lowPowerModeBatteryThresholdWatchTask,
						idleSleepTimerWatchTask,
						wirelessBrightnessWatchTask,
						mouseWatchTask,
						mouseDpiWatchTask,
						mouseDpiPresetWatchTask,
						mouseDpiPollingFrequencyWatchTask,
						monitorWatchTask,
						monitorSettingWatchTask,
						embeddedMonitorDeviceWatchTask,
						embeddedMonitorConfigurationWatchTask,
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

		_pendingEmbeddedMonitorDeviceInformations.Clear();
		_pendingEmbeddedMonitorConfigurationChanges.Clear();

		SelectedDevice = null;

		foreach (var device in _devices)
		{
			device.IsAvailable = false;
		}

		_devices.Clear();
	}

	private async Task WatchDevicesAsync
	(
		IDeviceService deviceService,
		IPowerService powerService,
		IMouseService mouseService,
		IEmbeddedMonitorService embeddedMonitorService,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await foreach (var notification in deviceService.WatchDevicesAsync(cancellationToken))
			{
				var id = notification.Details.Id;

				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					{
						var device = new DeviceViewModel
						(
							_connectionManager,
							_availableImages,
							_metadataService,
							powerService,
							mouseService,
							embeddedMonitorService,
							_rasterizationScaleProvider,
							notification.Details
						);
						await HandleDeviceArrivalAsync(device, cancellationToken);
						_devicesById.Add(notification.Details.Id, device);
						_devices.Add(device);
					}
					break;
				case WatchNotificationKind.Removal:
					_removedDeviceIds.Add(id);
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == id)
						{
							_devices.RemoveAt(i);
							_devicesById.Remove(id);
							HandleDeviceRemoval(device);
							break;
						}
					}
					break;
				case WatchNotificationKind.Update:
					for (int i = 0; i < _devices.Count; i++)
					{
						var device = _devices[i];
						if (device.Id == notification.Details.Id)
						{
							device.FriendlyName = notification.Details.FriendlyName;
							device.Category = notification.Details.Category;
							if (notification.Details.IsAvailable != device.IsAvailable)
							{
								if (device.IsAvailable = notification.Details.IsAvailable)
								{
									await HandleDeviceArrivalAsync(device, cancellationToken);
								}
								else
								{
									HandleDeviceRemoval(device);
								}
							}
							device.UpdateDeviceIds(notification.Details.DeviceIds, notification.Details.MainDeviceIdIndex);
							device.SerialNumber = notification.Details.SerialNumber;
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
	}

	private async Task WatchPowerDevicesAsync(IPowerService powerService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var information in powerService.WatchPowerDevicesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(information.DeviceId, out var device))
				{
					if (device.PowerFeatures is { } powerFeatures)
					{
						powerFeatures.UpdateInformation(information);
					}
				}
				else
				{
					_pendingPowerDeviceInformations[information.DeviceId] = information;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchBatteryChangesAsync(IPowerService powerService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in powerService.WatchBatteryChangesAsync(cancellationToken))
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
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchLowPowerModeBatteryThresholdChangesAsync(IPowerService powerService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var update in powerService.WatchLowPowerModeBatteryThresholdChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(update.DeviceId, out var device))
				{
					if (device.PowerFeatures is { } powerFeatures)
					{
						powerFeatures.UpdateLowPowerModeBatteryThreshold(update.BatteryThreshold);
					}
				}
				else
				{
					_pendingLowPowerModeBatteryThresholdChanges[update.DeviceId] = update.BatteryThreshold;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchIdleSleepTimerChangesAsync(IPowerService powerService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var update in powerService.WatchIdleSleepTimerChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(update.DeviceId, out var device))
				{
					if (device.PowerFeatures is { } powerFeatures)
					{
						powerFeatures.UpdateIdleSleepTimer(update.IdleTime);
					}
				}
				else
				{
					_pendingIdleSleepTimerChanges[update.DeviceId] = update.IdleTime;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchWirelessBrightnessChangesAsync(IPowerService powerService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var update in powerService.WatchWirelessBrightnessChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(update.DeviceId, out var device))
				{
					if (device.PowerFeatures is { } powerFeatures)
					{
						powerFeatures.UpdateWirelessBrightness(update.Brightness);
					}
				}
				else
				{
					_pendingWirelessBrightnessChanges[update.DeviceId] = update.Brightness;
				}
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception)
		{
		}
	}

	private async Task WatchMouseDevicesAsync(IMouseService mouseService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var mouseDevice in mouseService.WatchMouseDevicesAsync(cancellationToken))
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
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMouseDpiPresetsAsync(IMouseService mouseService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var dpiPresets in mouseService.WatchDpiPresetsAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(dpiPresets.DeviceId, out var device))
				{
					if (device.MouseFeatures is { } mouseFeatures)
					{
						mouseFeatures.UpdatePresets(dpiPresets.DpiPresets);
					}
				}
				else
				{
					_pendingDpiPresetChanges[dpiPresets.DeviceId] = dpiPresets.DpiPresets;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMouseDpiChangesAsync(IMouseService mouseService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in mouseService.WatchDpiChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MouseFeatures is { } mouseFeatures)
					{
						mouseFeatures.UpdateCurrentDpi(notification.PresetIndex, notification.Dpi);
					}
				}
				else
				{
					_pendingMouseDpiChanges[notification.DeviceId] = (notification.PresetIndex, notification.Dpi);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMousePollingFrequencyChangesAsync(IMouseService mouseService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in mouseService.WatchPollingFrequenciesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MouseFeatures is { } mouseFeatures)
					{
						mouseFeatures.UpdateCurrentPollingFrequency(notification.PollingFrequency);
					}
				}
				else
				{
					_pendingPollingFrequencyChanges[notification.DeviceId] = notification.PollingFrequency;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMonitorsAsync(IMonitorService monitorService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in monitorService.WatchMonitorsAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MonitorFeatures is { } monitorFeatures)
					{
						await monitorFeatures.UpdateInformationAsync(notification, cancellationToken);
					}
				}
				else
				{
					_pendingMonitorInformations[notification.DeviceId] = notification;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMonitorSettingChangesAsync(IMonitorService monitorService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in monitorService.WatchSettingsAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MonitorFeatures is { } monitorFeatures)
					{
						monitorFeatures.UpdateSetting(notification);
					}
				}
				else
				{
					if (!_pendingMonitorSettingChanges.TryGetValue(notification.DeviceId, out var changes))
					{
						_pendingMonitorSettingChanges[notification.DeviceId] = changes = [];
					}
					changes.Add(notification);
				}
			}
		}
		catch (OperationCanceledException)
		{
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
