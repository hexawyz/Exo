using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable
{
	private readonly IDeviceService _deviceService;
	private readonly ObservableCollection<DeviceViewModel> _devices;

	// Processing asynchronous status updates requires accessing the view model from the device ID.
	private readonly Dictionary<Guid, DeviceViewModel> _devicesById;
	// Used to store battery changes when the device view model is not accessible.
	private readonly Dictionary<Guid, BatteryStateViewModel> _pendingBatteryChanges;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice; 

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _deviceWatchTask;
	private readonly Task _batteryWatchTask;

	public DevicesViewModel(IDeviceService deviceService)
	{
		_deviceService = deviceService;
		_devices = new();
		_devicesById = new();
		_pendingBatteryChanges = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_deviceWatchTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_batteryWatchTask = WatchBatteryChangesAsync(_cancellationTokenSource.Token);
	}

	private async Task WatchDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceService.WatchDevicesAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Arrival:
					{
						ExtendedDeviceInformation extendedDeviceInformation;
						try
						{
							extendedDeviceInformation = await _deviceService.GetExtendedDeviceInformationAsync(new() { Id = notification.Details.Id }, cancellationToken);
						}
						catch (Exception ex) when (ex is not OperationCanceledException)
						{
							// Exceptions here would likely be caused by a driver removal.
							// Disconnection from the service is not yet handled.
							continue;
						}
						var device = new DeviceViewModel(notification.Details, extendedDeviceInformation);
						if (_pendingBatteryChanges.Remove(notification.Details.Id, out var batteryStatus))
						{
							device.BatteryState = batteryStatus;
						}
						_devicesById.Add(notification.Details.Id, device);
						_devices.Add(device);
					}
					break;
				case WatchNotificationKind.Removal:
					for (int i = 0; i < _devices.Count; i++)
					{
						if (_devices[i].Id == notification.Details.Id)
						{
							_devices.RemoveAt(i);
							_devicesById.Remove(notification.Details.Id);
							_pendingBatteryChanges.Remove(notification.Details.Id, out _);
							break;
						}
					}
					break;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task WatchBatteryChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceService.WatchBatteryChangesAsync(cancellationToken))
			{
				var status = new BatteryStateViewModel(notification);
				if (_devicesById.TryGetValue(notification.DeviceId, out var device))
				{
					device.BatteryState = status;
				}
				else
				{
					_pendingBatteryChanges.Add(notification.DeviceId, status);
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
	}

	public DeviceViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set => SetValue(ref _selectedDevice, value);
	}
}
