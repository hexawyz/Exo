using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Exo.Contracts.Ui;
using Exo.Ui;
using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable
{
	private readonly ObservableCollection<DeviceViewModel> _devices;

	// All removed device IDs are definitely removed. Device removal is a manual request from the user, and can only happen when the device is disconnected.
	// If the same device is reconnected later, it will be considered a new device and get a new and device ID.
	private readonly HashSet<Guid> _removedDeviceIds;

	private readonly SettingsServiceConnectionManager _connectionManager;

	// Processing asynchronous status updates requires accessing the view model from the device ID.
	private readonly Dictionary<Guid, DeviceViewModel> _devicesById;

	// Used to store battery changes when the device view model is not accessible.
	private readonly Dictionary<Guid, BatteryStateViewModel> _pendingBatteryChanges;

	// Used to store DPI changes when the device view model is not accessible.
	private readonly Dictionary<Guid, DpiViewModel> _pendingDpiChanges;

	// Used to store monitor setting changes when the device view model is not accessible.
	private readonly Dictionary<Guid, List<MonitorSettingValue>> _pendingMonitorSettingChanges;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice; 

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _deviceWatchTask;
	private readonly Task _batteryWatchTask;
	private readonly Task _dpiWatchTask;
	private readonly Task _monitorSettingWatchTask;

	public DevicesViewModel(SettingsServiceConnectionManager connectionManager)
	{
		_devices = new();
		_removedDeviceIds = new();
		_connectionManager = connectionManager;
		_devicesById = new();
		_pendingBatteryChanges = new();
		_pendingDpiChanges = new();
		_pendingMonitorSettingChanges = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_deviceWatchTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_batteryWatchTask = WatchBatteryChangesAsync(_cancellationTokenSource.Token);
		_dpiWatchTask = WatchDpiChangesAsync(_cancellationTokenSource.Token);
		_monitorSettingWatchTask = WatchMonitorSettingChangesAsync(_cancellationTokenSource.Token);
	}

	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var deviceService = await _connectionManager.GetDeviceServiceAsync(cancellationToken);
			await foreach (var notification in deviceService.WatchDevicesAsync(cancellationToken))
			{
				var id = notification.Details.Id;

				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					{
						//ExtendedDeviceInformation extendedDeviceInformation;
						//try
						//{
						//	extendedDeviceInformation = await _deviceService.GetExtendedDeviceInformationAsync(new() { Id = notification.Details.Id }, cancellationToken);
						//}
						//catch (Exception ex) when (ex is not OperationCanceledException)
						//{
						//	// Exceptions here would likely be caused by a driver removal.
						//	// Disconnection from the service is not yet handled.
						//	continue;
						//}
						var device = new DeviceViewModel(_connectionManager, notification.Details);
						HandleDeviceArrival(device);
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
									HandleDeviceArrival(device);
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

	private void HandleDeviceArrival(DeviceViewModel device)
	{
		if (_pendingBatteryChanges.Remove(device.Id, out var batteryStatus))
		{
			device.BatteryState = batteryStatus;
		}
		if (_pendingDpiChanges.Remove(device.Id, out var dpi))
		{
			if (device.MouseFeatures is { } mouseFeatures)
			{
				mouseFeatures.CurrentDpi = dpi;
			}
		}
		if (_pendingMonitorSettingChanges.Remove(device.Id, out var monitorSettings))
		{
			if (device.MonitorFeatures is { } monitorFeatures)
			{
				foreach (var setting in monitorSettings)
				{
					monitorFeatures.UpdateSetting(setting);
				}
			}
		}
	}

	private void HandleDeviceRemoval(DeviceViewModel device)
	{
		device.BatteryState = null;
		_pendingBatteryChanges.Remove(device.Id, out _);
	}

	private async Task WatchBatteryChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var deviceService = await _connectionManager.GetDeviceServiceAsync(cancellationToken);
			await foreach (var notification in deviceService.WatchBatteryChangesAsync(cancellationToken))
			{
				var status = new BatteryStateViewModel(notification);
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					device.BatteryState = status;
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

	private async Task WatchDpiChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var mouseService = await _connectionManager.GetMouseServiceAsync(cancellationToken);
			await foreach (var notification in mouseService.WatchDpiChangesAsync(cancellationToken))
			{
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					if (device.MouseFeatures is { } mouseFeatures)
					{
						mouseFeatures.CurrentDpi = new(notification.Dpi);
					}
				}
				else
				{
					_pendingDpiChanges.Add(notification.DeviceId, new(notification.Dpi));
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchMonitorSettingChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			var monitorService = await _connectionManager.GetMonitorServiceAsync(cancellationToken);
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
						_pendingMonitorSettingChanges.Add(notification.DeviceId, changes = new());
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

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _deviceWatchTask.ConfigureAwait(false);
		await _batteryWatchTask.ConfigureAwait(false);
		await _dpiWatchTask.ConfigureAwait(false);
		await _monitorSettingWatchTask.ConfigureAwait(false);
	}

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
