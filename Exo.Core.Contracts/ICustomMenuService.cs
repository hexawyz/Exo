using System.ServiceModel;

namespace Exo.Core.Contracts;

[ServiceContract]
public interface ICustomMenuService
{
	[OperationContract]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);
}
