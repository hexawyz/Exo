using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcDeviceService : IDeviceService
{
	private readonly DriverRegistry _driverRegistry;

	public GrpcDeviceService(DriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAsync(cancellationToken))
		{
			yield return new
			(
				notification.Kind.ToGrpc(),
				notification.DeviceInformation.ToGrpc()
			);
		}
	}
}
