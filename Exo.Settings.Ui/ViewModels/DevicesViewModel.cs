using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable
{
	private readonly ObservableCollection<DeviceViewModel> _devices;

	private readonly IDeviceService _deviceService;
	private readonly IMouseService _mouseService;

	// Processing asynchronous status updates requires accessing the view model from the device ID.
	private readonly Dictionary<Guid, DeviceViewModel> _devicesById;

	// Used to store battery changes when the device view model is not accessible.
	private readonly Dictionary<Guid, BatteryStateViewModel> _pendingBatteryChanges;

	// Used to store DPI changes when the device view model is not accessible.
	private readonly Dictionary<Guid, DpiViewModel> _pendingDpiChanges;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice; 

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _deviceWatchTask;
	private readonly Task _batteryWatchTask;
	private readonly Task _dpiWatchTask;

	public DevicesViewModel(IDeviceService deviceService, IMouseService mouseService)
	{
		_deviceService = deviceService;
		_mouseService = mouseService;

		_devices = new();
		_devicesById = new();
		_pendingBatteryChanges = new();
		_pendingDpiChanges = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_deviceWatchTask = WatchDevicesAsync(_cancellationTokenSource.Token);
		_batteryWatchTask = WatchBatteryChangesAsync(_cancellationTokenSource.Token);
		_dpiWatchTask = WatchDpiChangesAsync(_cancellationTokenSource.Token);
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
						if (_pendingDpiChanges.Remove(notification.Details.Id, out var dpi))
						{
							if (device.MouseFeatures is { } mouseFeatures)
							{
								mouseFeatures.CurrentDpi = dpi;
							}
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
			return;
		}
		catch
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
			await foreach (var notification in _mouseService.WatchDpiChangesAsync(cancellationToken))
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
