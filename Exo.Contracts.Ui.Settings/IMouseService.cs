using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Mouse")]
public interface IMouseService
{
	[OperationContract]
	IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync(CancellationToken cancellationToken);
}
