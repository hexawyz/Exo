using System.Collections.Generic;
using System.Threading;

namespace Exo.Service;

public interface IDeviceWatcher
{
	IAsyncEnumerable<DeviceWatchNotification> WatchAsync(CancellationToken cancellationToken);
	IAsyncEnumerable<DeviceWatchNotification> WatchAsync<TFeature>(CancellationToken cancellationToken) where TFeature : class, IDeviceFeature;
}
