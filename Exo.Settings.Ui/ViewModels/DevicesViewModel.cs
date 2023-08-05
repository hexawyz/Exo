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
					_devices.Add(new(notification.Details));
					break;
				case WatchNotificationKind.Removal:
					for (int i = 0; i < _devices.Count; i++)
					{
						if (_devices[i].DeviceId == notification.Details.DeviceId)
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
}
