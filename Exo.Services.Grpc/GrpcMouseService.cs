using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;
using GrpcMouseDeviceInformation = Exo.Contracts.Ui.Settings.MouseDeviceInformation;

namespace Exo.Service.Grpc;

internal sealed class GrpcMouseService : IMouseService
{
	private readonly MouseService _mouseService;
	private readonly ILogger<GrpcMouseService> _logger;

	public GrpcMouseService(ILogger<GrpcMouseService> logger, MouseService mouseService)
	{
		_mouseService = mouseService;
		_logger = logger;
	}

	public async IAsyncEnumerable<GrpcMouseDeviceInformation> WatchMouseDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var mouseDevice in _mouseService.WatchMouseDevicesAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return mouseDevice.ToGrpc();
		}
	}

	public async IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcMouseServiceDpiWatchStart();
		try
		{
			await foreach (var notification in _mouseService.WatchDpiChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				if (notification.NotificationKind == WatchNotificationKind.Removal) continue;

				yield return new()
				{
					DeviceId = notification.DeviceId,
					Dpi = notification.NewValue.Dpi.ToGrpc(),
				};
			}
		}
		finally
		{
			_logger.GrpcMouseServiceDpiWatchStop();
		}
	}

	public IAsyncEnumerable<MouseDpiPresets> WatchDpiPresetsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	public ValueTask SetDpiPresetsAsync(MouseDpiPresets request, CancellationToken cancellationToken) => throw new NotImplementedException();
}
