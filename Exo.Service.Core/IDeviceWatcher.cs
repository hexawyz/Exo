using System.Collections.Generic;
using System.Threading;

namespace Exo.Service;

public interface IDeviceWatcher
{
	IAsyncEnumerable<DeviceWatchNotification> WatchAllAsync(CancellationToken cancellationToken);
	IAsyncEnumerable<DeviceWatchNotification> WatchAllAsync<TFeature>(CancellationToken cancellationToken) where TFeature : class, IDeviceFeature;

	IAsyncEnumerable<DeviceWatchNotification> WatchAvailableAsync(CancellationToken cancellationToken);
	IAsyncEnumerable<DeviceWatchNotification> WatchAvailableAsync<TFeature>(CancellationToken cancellationToken) where TFeature : class, IDeviceFeature;
}
