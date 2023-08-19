using System.ServiceModel;

namespace Exo.Ui.Contracts;

[ServiceContract(Name = "Mouse")]
public interface IMouseService
{
	[OperationContract]
	IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync(CancellationToken cancellationToken);
}
