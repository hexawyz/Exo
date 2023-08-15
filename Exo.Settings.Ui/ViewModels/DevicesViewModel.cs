using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DevicesViewModel : BindableObject, IAsyncDisposable
{
	private readonly IDeviceService _deviceService;
	private readonly ObservableCollection<DeviceViewModel> _devices;

	// The selected device is the device currently being observed.
	private DeviceViewModel? _selectedDevice; 

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public DevicesViewModel(IDeviceService deviceService)
	{
		_deviceService = deviceService;
		_devices = new();
		_cancellationTokenSource = new CancellationTokenSource();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceService.WatchDevicesAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Arrival:
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
					_devices.Add(new(notification.Details, extendedDeviceInformation));
					break;
				case WatchNotificationKind.Removal:
					for (int i = 0; i < _devices.Count; i++)
					{
						if (_devices[i].Id == notification.Details.Id)
						{
							_devices.RemoveAt(i);
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

	public ObservableCollection<DeviceViewModel> Devices => _devices;

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
	}

	public DeviceViewModel? SelectedDevice
	{
		get => _selectedDevice;
		set => SetValue(ref _selectedDevice, value);
	}
}
