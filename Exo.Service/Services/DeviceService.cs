using System;
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
					notification.DeviceInformation.UniqueId,
					notification.DeviceInformation.FriendlyName,
					notification.DeviceInformation.DriverType.ToString(),
					Array.ConvertAll(notification.DeviceInformation.FeatureTypes, t => t.ToString())
				)
			);
		}
	}
}
