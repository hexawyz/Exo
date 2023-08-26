using System.ServiceModel;

namespace Exo.Overlay.Contracts;

[ServiceContract]
public interface IOverlayNotificationService
{
	[OperationContract]
	IAsyncEnumerable<OverlayRequest> WatchOverlayRequestsAsync(CancellationToken cancellationToken);
}
