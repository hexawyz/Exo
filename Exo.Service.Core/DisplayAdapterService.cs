using Exo.Features;
using Exo.I2C;

namespace Exo.Service;

internal class DisplayAdapterService
{
	private CancellationTokenSource? _cancellationTokenSource = new();
	private readonly IDeviceWatcher _deviceWatcher;
	private readonly II2cBusRegistry _busRegistry;
	private readonly Task _displayAdapterWatchTask;

	public DisplayAdapterService(IDeviceWatcher deviceWatcher, II2cBusRegistry busRegistry)
	{
		_deviceWatcher = deviceWatcher;
		_busRegistry = busRegistry;
		_displayAdapterWatchTask = WatchDisplayAdaptersAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _displayAdapterWatchTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchDisplayAdaptersAsync(CancellationToken cancellationToken)
	{
		// This method is used to automatically register and unregister the I2C implementations of display adapters that will be used by monitor drivers.
		var busRegistrations = new Dictionary<Guid, IDisposable>();
		try
		{
			var settings = new List<MonitorSetting>();

			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<IDisplayAdapterDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Addition:
						var displayAdapterFeatures = (IDeviceFeatureSet<IDisplayAdapterDeviceFeature>)notification.FeatureSet!;
						if (displayAdapterFeatures.GetFeature<IDisplayAdapterI2cBusProviderFeature>() is { } busFeature)
						{
							busRegistrations.Add(notification.DeviceInformation.Id, _busRegistry.RegisterBusResolver(busFeature.DeviceName, busFeature.GetBusForMonitorAsync));
						}
						break;
					case WatchNotificationKind.Removal:
						if (busRegistrations.Remove(notification.DeviceInformation.Id, out var registration))
						{
							registration.Dispose();
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
}
