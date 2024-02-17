using System.Runtime.CompilerServices;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

public class GrpcMouseService : IMouseService
{
	private readonly DeviceRegistry _driverRegistry;
	private readonly DpiWatcher _dpiWatcher;

	public GrpcMouseService(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
		_dpiWatcher = new DpiWatcher(driverRegistry);
	}

	//public async IAsyncEnumerable<WatchNotification<MouseDeviceInformation>> WatchMouseDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	//{
	//	await foreach (var notification in _driverRegistry.WatchAsync<IMouseDeviceFeature>(cancellationToken).ConfigureAwait(false))
	//	{
	//		switch (notification.Kind)
	//		{
	//		case WatchNotificationKind.Enumeration:
	//		case WatchNotificationKind.Addition:
	//			yield return new()
	//			{
	//				NotificationKind = notification.Kind.ToGrpc(),
	//				Details = new()
	//				{
	//					DeviceInformation = notification.DeviceInformation.ToGrpc(),
	//					ButtonCount = 3,
	//					HasSeparableDpi = false,
	//					MaximumDpi = new() { Horizontal = 1000, Vertical = 1000 },
	//				},
	//			};
	//			break;
	//		case WatchNotificationKind.Removal:
	//			break;
	//		}
	//	}
	//}

	public async IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _dpiWatcher.WatchAsync(cancellationToken).ConfigureAwait(false))
		{
			if (notification.NotificationKind == WatchNotificationKind.Removal) continue;

			yield return new()
			{
				DeviceId = notification.DeviceId,
				Dpi = new() { Horizontal = notification.NewValue.Dpi.Horizontal, Vertical = notification.NewValue.Dpi.Vertical }
			};
		}
	}
}
