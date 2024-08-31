using System.ServiceModel;

namespace Exo.Contracts.Ui.Overlay;

[ServiceContract(Name = "OverlayNotification")]
public interface IOverlayNotificationService
{
	[OperationContract(Name = "WatchRequests")]
	IAsyncEnumerable<OverlayRequest> WatchOverlayRequestsAsync(CancellationToken cancellationToken);
}
