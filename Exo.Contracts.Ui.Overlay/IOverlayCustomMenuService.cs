using System.ServiceModel;

namespace Exo.Contracts.Ui.Overlay;

[ServiceContract(Name = "OverlayCustomMenu")]
public interface IOverlayCustomMenuService
{
	[OperationContract(Name = "WatchMenuChanges")]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "InvokeMenuItem")]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);
}
