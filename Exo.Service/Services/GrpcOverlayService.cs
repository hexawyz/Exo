using Exo.Contracts.Ui.Overlay;

namespace Exo.Service.Services;

internal class GrpcOverlayNotificationService : IOverlayNotificationService
{
	private readonly OverlayNotificationService _overlayNotificationService;

	public GrpcOverlayNotificationService(OverlayNotificationService overlayNotificationService) => _overlayNotificationService = overlayNotificationService;

	public IAsyncEnumerable<OverlayRequest> WatchOverlayRequestsAsync(CancellationToken cancellationToken)
		=> _overlayNotificationService.WatchOverlayRequestsAsync(cancellationToken);
}
