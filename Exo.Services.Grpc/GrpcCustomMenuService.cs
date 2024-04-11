using System.Runtime.CompilerServices;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Overlay;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Grpc;

internal sealed class GrpcCustomMenuService : IOverlayCustomMenuService, ISettingsCustomMenuService
{
	private readonly CustomMenuService _customMenuService;

	public GrpcCustomMenuService(CustomMenuService customMenuService) => _customMenuService = customMenuService;

	public async IAsyncEnumerable<MenuChangeNotification> WatchMenuChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _customMenuService.WatchChangesAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new MenuChangeNotification()
			{
				Kind = notification.Kind.ToGrpc(),
				ParentItemId = notification.ParentItemId,
				Position = (uint)notification.Position,
				ItemId = notification.MenuItem.ItemId,
				ItemType = notification.MenuItem.Type,
				Text = (notification.MenuItem as TextMenuItem)?.Text
			};
		}
	}

	public ValueTask InvokeMenuItemAsync(MenuItemReference menuItemReference, CancellationToken cancellationToken)
	{
		return ValueTask.CompletedTask;
	}

	public ValueTask UpdateMenuAsync(MenuDefinition menuDefinition, CancellationToken cancellationToken)
	{
		return ValueTask.CompletedTask;
	}
}
