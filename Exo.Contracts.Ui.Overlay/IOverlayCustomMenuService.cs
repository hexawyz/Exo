using System.ServiceModel;

namespace Exo.Contracts.Ui.Overlay;

[ServiceContract]
public interface IOverlayCustomMenuService
{
	[OperationContract]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);
}
