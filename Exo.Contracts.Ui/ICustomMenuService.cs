using System.ServiceModel;

namespace Exo.Contracts.Ui;

[ServiceContract]
public interface ICustomMenuService
{
	[OperationContract]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);
}
