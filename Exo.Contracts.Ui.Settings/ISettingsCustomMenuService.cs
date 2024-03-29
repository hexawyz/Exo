using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract]
public interface ISettingsCustomMenuService
{
	[OperationContract]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);
}
