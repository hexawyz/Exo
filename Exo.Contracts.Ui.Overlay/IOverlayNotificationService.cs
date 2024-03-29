using System.ServiceModel;

namespace Exo.Contracts.Ui.Overlay;

[ServiceContract]
public interface IOverlayNotificationService
{
	[OperationContract]
	IAsyncEnumerable<OverlayRequest> WatchOverlayRequestsAsync(CancellationToken cancellationToken);
}
