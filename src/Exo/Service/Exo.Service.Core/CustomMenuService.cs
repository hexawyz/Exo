using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed class CustomMenuService
{

	public static async ValueTask<CustomMenuService> CreateAsync(ILogger<CustomMenuService> logger, IConfigurationContainer configurationContainer, CancellationToken cancellationToken)
	{
		var result = await configurationContainer.ReadValueAsync(SourceGenerationContext.Default.MenuConfiguration, cancellationToken).ConfigureAwait(false);

		MenuConfiguration configuration;
		if (result.Found && !result.Value.MenuItems.IsDefault)
		{
			configuration = result.Value;
		}
		else
		{
			configuration = new MenuConfiguration
			{
				MenuItems =
				[
					new TextMenuItem(new Guid(0x3BC23D78, 0x5237, 0x4BD6, 0xB1, 0xB5, 0xD5, 0xCA, 0x65, 0xFE, 0x73, 0x20), "Custom command &1"),
					new TextMenuItem(new Guid(0x8D93A224, 0x1B57, 0x47AA, 0xBA, 0xBA, 0x4A, 0x3E, 0xEB, 0xB4, 0x4F, 0x38), "Custom command &2"),
					new SubMenuMenuItem
					(
						new Guid(0xCE29B514, 0x06AC, 0x48E1, 0x94, 0x8A, 0x63, 0xEE, 0x62, 0x84, 0x24, 0xB0),
						"C&ustom submenu",
						[
							new TextMenuItem(new Guid(0x5E2FC2AB, 0x4E1C, 0x4851, 0x82, 0x6F, 0x9A, 0x8A, 0xC0, 0x1C, 0xAC, 0x37), "Custom command &3"),
							new TextMenuItem(new Guid(0x918D2C08, 0x8109, 0x4752, 0x98, 0x02, 0x4F, 0x4D, 0x86, 0x56, 0x84, 0x78), "Custom command &4"),
						]
					),
				],
			};

			await configurationContainer.WriteValueAsync(configuration, SourceGenerationContext.Default.MenuConfiguration, cancellationToken).ConfigureAwait(false);
		}

		return new CustomMenuService(logger, configurationContainer, configuration);
	}

	private readonly IConfigurationContainer _configurationContainer;
	private MenuItem[] _menuItems;
	private readonly Lock _lock;
	private ChannelWriter<MenuItemWatchNotification>[]? _changeListeners;
	private readonly ILogger<CustomMenuService> _logger;

	private CustomMenuService(ILogger<CustomMenuService> logger, IConfigurationContainer configurationContainer, MenuConfiguration configuration)
	{
		_logger = logger;
		_configurationContainer = configurationContainer;
		_menuItems = ImmutableCollectionsMarshal.AsArray(configuration.MenuItems)!;
		_lock = new();
	}

	public ValueTask UpdateMenuAsync(ImmutableArray<MenuItem> menuItems, CancellationToken cancellationToken)
	{
		if (menuItems.IsDefault) throw new ArgumentNullException(nameof(menuItems));
		return UpdateMenuAsync(ImmutableCollectionsMarshal.AsArray(menuItems)!, cancellationToken);
	}

	private async ValueTask UpdateMenuAsync(MenuItem[] menuItems, CancellationToken cancellationToken)
	{
		if (menuItems is null) throw new ArgumentNullException(nameof(menuItems));

		// This implement a diff algorithm for hierarchical menus.
		// We can never be perfect about differences, but at a given depth level, we can always detect moved items using their unique ID.
		// If an item with children is removed, we ignore all the children. Items with these ID can be moved anywhere and will be considered as new items.
		var pendingMenuComparisons = new Queue<(Guid ParentItemId, MenuItem[] OldItems, MenuItem[] NewItems)>();
		var pendingNotifications = new Queue<MenuItemWatchNotification>();
		var currentMenuItemsById = new Dictionary<Guid, int>();
		var updatedItemPositions = new List<int>(Math.Max(menuItems.Length, 10));
		lock (_lock)
		{
			pendingMenuComparisons.Enqueue((Constants.RootMenuItem, _menuItems, menuItems));

			while (pendingMenuComparisons.TryDequeue(out var t))
			{
				var (parentItemId, oldItems, newItems) = t;
				currentMenuItemsById.Clear();
				updatedItemPositions.Clear();

				// First reference the new items.
				// This is important because it will allow us to detect potentially removed items afterwards:
				// For now, we don't know if any of those items are old or new, moved or unmoved.
				// Move can be an implicit consequence of a remove, but we'll detect that when we identify removed items.
				for (int i = 0; i < newItems.Length; i++)
				{
					var newItem = newItems[i];
					currentMenuItemsById.Add(newItem.ItemId, i);
				}

				// In this next loop, we identify removed items and track the new indices after removals, then prepare the removal notifications.
				// Notifications for items that have been moved or changed will be sent afterwards, because they will need to be sorted by position.
				int runningIndex = 0;
				for (int i = 0; i < oldItems.Length; i++)
				{
					var oldItem = oldItems[i];
					if (!currentMenuItemsById.Remove(oldItem.ItemId, out var newItemPosition))
					{
						pendingNotifications.Enqueue
						(
							new()
							{
								Kind = WatchNotificationKind.Removal,
								ParentItemId = parentItemId,
								Position = runningIndex,
								MenuItem = oldItem
							}
						);
					}
					else
					{
						var newMenuItem = newItems[newItemPosition];
						if (runningIndex != newItemPosition || !oldItem.NonRecursiveEquals(newMenuItem))
						{
							updatedItemPositions.Add(newItemPosition);
						}

						var oldSubMenuItems = GetSubMenuItems(oldItem);
						var newSubMenuItems = GetSubMenuItems(newMenuItem);

						if ((oldSubMenuItems.Length | newSubMenuItems.Length) != 0)
						{
							pendingMenuComparisons.Enqueue((oldItem.ItemId, oldSubMenuItems, newSubMenuItems));
						}

						runningIndex++;
					}
				}

				// Once all removed, changed, and moved items have been identified, we can aggregate the added items to the list of updates.
				foreach (var addedItemPosition in currentMenuItemsById.Values)
				{
					updatedItemPositions.Add(addedItemPosition);
					// If we added a new submenu, we need to push the added menu items too.
					// There are other ways (more optimal) to do this, but this will do the job.
					var newItem = newItems[addedItemPosition];
					if (newItem.Type == MenuItemType.SubMenu) pendingMenuComparisons.Enqueue((newItem.ItemId, [], GetSubMenuItems(newItem)));
				}

				// Sort the updates by position:
				// If we send the updates in (new) position order, we are guaranteed to not mix up anything.
				// Items will be automatically moved, updated or inserted in proper order.
				updatedItemPositions.Sort();

				foreach (int updatedItemPosition in updatedItemPositions)
				{
					var newItem = newItems[updatedItemPosition];

					pendingNotifications.Enqueue
					(
						new()
						{
							Kind = currentMenuItemsById.ContainsKey(newItem.ItemId) ? WatchNotificationKind.Addition : WatchNotificationKind.Update,
							ParentItemId = parentItemId,
							Position = updatedItemPosition,
							MenuItem = newItem,
						}
					);
				}
			}

			// Publish the actual menu item update before pushing the notifications.
			_menuItems = menuItems;

			foreach (var notification in pendingNotifications)
			{
				_changeListeners.TryWrite(notification);
			}
		}

		await _configurationContainer.WriteValueAsync(new MenuConfiguration { MenuItems = ImmutableCollectionsMarshal.AsImmutableArray(menuItems) }, SourceGenerationContext.Default.MenuConfiguration, cancellationToken).ConfigureAwait(false);

		static MenuItem[] GetSubMenuItems(MenuItem menuItem) => (menuItem is SubMenuMenuItem smmi ? ImmutableCollectionsMarshal.AsArray(smmi.MenuItems) : null) ?? [];
	}

	public async IAsyncEnumerable<MenuItemWatchNotification> WatchChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<MenuItemWatchNotification>();

		MenuItem[] menuItems;
		lock (_lock)
		{
			menuItems = _menuItems;
			ArrayExtensions.InterlockedAdd(ref _changeListeners, channel);
		}

		try
		{
			Queue<SubMenuMenuItem>? subMenus = null;
			Guid parentItemId = Constants.RootMenuItem;
			while (true)
			{
				for (int i = 0; i < menuItems.Length; i++)
				{
					var menuItem = menuItems[i];
					yield return new MenuItemWatchNotification
					{
						Kind = WatchNotificationKind.Enumeration,
						ParentItemId = parentItemId,
						Position = i,
						MenuItem = menuItem,
					};
					if (menuItem is SubMenuMenuItem subMenuMenuItem)
					{
						(subMenus ??= new()).Enqueue(subMenuMenuItem);
					}
				}

				if (subMenus is null || subMenus.Count == 0) break;

				var subMenu = subMenus.Dequeue();
				menuItems = ImmutableCollectionsMarshal.AsArray(subMenu.MenuItems)!;
				parentItemId = subMenu.ItemId;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _changeListeners, channel);
		}
	}
}
