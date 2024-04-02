using System.Runtime.CompilerServices;
using Exo.Features;
using Exo.I2C;
using Exo.SystemManagementBus;

namespace Exo.Service;

internal class MotherboardService
{
	private CancellationTokenSource? _cancellationTokenSource = new();
	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ISystemManagementBusRegistry _busRegistry;
	private readonly Task _motherboardWatchTask;

	public MotherboardService(IDeviceWatcher deviceWatcher, ISystemManagementBusRegistry busRegistry)
	{
		_deviceWatcher = deviceWatcher;
		_busRegistry = busRegistry;
		_motherboardWatchTask = WatchDisplayAdaptersAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _motherboardWatchTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchDisplayAdaptersAsync(CancellationToken cancellationToken)
	{
		// This method is used to automatically register and unregister the SMBus implementations of motherboard drivers.
		// There should be only one motherboard driver in existence, and it should never cease to exist, but it is better to write the code correctly here too.
		var busRegistrations = new Dictionary<Guid, IDisposable>();
		try
		{
			var settings = new List<MonitorSetting>();

			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<IMotherboardDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Addition:
						var motherboardFeatures = (IDeviceFeatureSet<IMotherboardDeviceFeature>)notification.FeatureSet!;
						if (motherboardFeatures.GetFeature<IMotherboardSystemManagementBusFeature>() is { } busFeature)
						{
							busRegistrations.Add(notification.DeviceInformation.Id, _busRegistry.RegisterSystemBus(busFeature));
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
