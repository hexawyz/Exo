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
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public DevicesViewModel(IDeviceService deviceService)
	{
		_deviceService = deviceService;
		_devices = new ObservableCollection<DeviceViewModel>();
		_cancellationTokenSource = new CancellationTokenSource();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceService.GetDevicesAsync(cancellationToken))
			{
				switch (notification.NotificationKind)
				{
				case DeviceNotificationKind.Enumeration:
				case DeviceNotificationKind.Arrival:
					_devices.Add(new(notification.DeviceInformation));
					break;
				case DeviceNotificationKind.Removal:
					for (int i = 0; i < _devices.Count; i++)
					{
						if (_devices[i].UniqueId == notification.DeviceInformation.UniqueId)
						{
							_devices.RemoveAt(i);
							break;
						}
					}
					break;
				}
			}
		}
		catch (Exception ex)
		{
		}
	}

	public ObservableCollection<DeviceViewModel> Devices => _devices;

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
	}
}
