using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "SettingsCustomMenu")]
public interface ISettingsCustomMenuService
{
	[OperationContract(Name = "WatchMenuChanges")]
	IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "InvokeMenuItem")]
	ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken);

	[OperationContract(Name = "UpdateMenu")]
	ValueTask UpdateMenuAsync(MenuDefinition menuDefinition, CancellationToken cancellationToken);
}
