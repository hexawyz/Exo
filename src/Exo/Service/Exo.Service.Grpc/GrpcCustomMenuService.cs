using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

	public async ValueTask UpdateMenuAsync(MenuDefinition menuDefinition, CancellationToken cancellationToken)
	{
		await _customMenuService.UpdateMenuAsync(ConvertMenu(menuDefinition.MenuItems), cancellationToken).ConfigureAwait(false);
	}

	private readonly struct IntermediateMenuDefinition
	{
		public Guid ItemId { get; }
		public string Text { get; }
		public ImmutableArray<MenuItemDefinition> MenuItems { get; }

		public IntermediateMenuDefinition(Guid itemId, string text, ImmutableArray<MenuItemDefinition> menuItems)
		{
			ItemId = itemId;
			Text = text;
			MenuItems = menuItems;
		}
	}

	private static ImmutableArray<MenuItem> ConvertMenu(ImmutableArray<MenuItemDefinition> menuItems)
	{
		static MenuItem[] CreateArray(int count)
			=> count > 0 ? new MenuItem[count] : [];

		var stack = new Stack<(IntermediateMenuDefinition currentMenu, int Index, MenuItem[] ConvertedItems)> { };
		var currentMenu = new IntermediateMenuDefinition(Constants.RootMenuItem, "", menuItems);
		int index = 0;
		var convertedItems = CreateArray(currentMenu.MenuItems.Length);
		while (true)
		{
			if (index < currentMenu.MenuItems.Length)
			{
				var item = currentMenu.MenuItems[index];
				if (item.Type != MenuItemType.Separator && item.Text is not { Length: > 0 }) throw new ArgumentException("Menu items should have a valid text.");
				switch (item.Type)
				{
				case MenuItemType.Default:
					convertedItems[index++] = new TextMenuItem(item.ItemId, item.Text!);
					break;
				case MenuItemType.SubMenu:
					stack.Push((currentMenu, index, convertedItems));
					currentMenu = new(item.ItemId, item.Text!, item.MenuItems);
					index = 0;
					convertedItems = CreateArray(currentMenu.MenuItems.Length);
					break;
				case MenuItemType.Separator:
					convertedItems[index++] = new SeparatorMenuItem(item.ItemId);
					break;
				default:
					throw new InvalidOperationException();
				}
			}
			else if (stack.Count == 0)
			{
				return ImmutableCollectionsMarshal.AsImmutableArray(convertedItems);
			}
			else
			{
				var convertedItem = new SubMenuMenuItem(currentMenu!.ItemId, currentMenu.Text!, ImmutableCollectionsMarshal.AsImmutableArray(convertedItems));

				(currentMenu, index, convertedItems) = stack.Pop();

				convertedItems[index++] = convertedItem;
			}
		}
	}
}
