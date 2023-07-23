using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

public class DeviceService : IDeviceService
{
	private readonly DriverRegistry _driverRegistry;

	public DeviceService(DriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public async IAsyncEnumerable<DeviceNotification> GetDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAsync(cancellationToken))
		{
			yield return new
			(
				notification.Kind switch
				{
					DriverWatchNotificationKind.Enumeration => DeviceNotificationKind.Enumeration,
					DriverWatchNotificationKind.Addition => DeviceNotificationKind.Arrival,
					DriverWatchNotificationKind.Removal => DeviceNotificationKind.Removal,
					_ => throw new System.NotImplementedException()
				},
				new
				(
					notification.Driver.ConfigurationKey.DeviceMainId,
					notification.Driver.FriendlyName,
					notification.GetType().FullName,
					new string[0]
				)
			);
		}
	}
}
