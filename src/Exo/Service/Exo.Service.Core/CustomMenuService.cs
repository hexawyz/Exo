using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts.Ui;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed class CustomMenuService
{
	[TypeId(0xA1D958FA, 0x6B89, 0x45BF, 0xB2, 0xDD, 0xA2, 0x36, 0x8A, 0xCF, 0x2F, 0x26)]
	private readonly struct MenuConfiguration
	{
		public required ImmutableArray<MenuItem> MenuItems { get; init; } = [];

		public MenuConfiguration() { }
	}

	public static async ValueTask<CustomMenuService> CreateAsync(ILogger<CustomMenuService> logger, IConfigurationContainer configurationContainer, CancellationToken cancellationToken)
	{
		var result = await configurationContainer.ReadValueAsync<MenuConfiguration>(cancellationToken).ConfigureAwait(false);

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

			await configurationContainer.WriteValueAsync(configuration, cancellationToken).ConfigureAwait(false);
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
							Position = runningIndex,
							MenuItem = newItem,
						}
					);
				}
			}

			// Push the actual menu item update before pushing the notifications.
			_menuItems = menuItems;

			foreach (var notification in pendingNotifications)
			{
				_changeListeners.TryWrite(notification);
			}
		}

		await _configurationContainer.WriteValueAsync(new MenuConfiguration { MenuItems = ImmutableCollectionsMarshal.AsImmutableArray(menuItems) }, cancellationToken).ConfigureAwait(false);

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

public readonly struct MenuItemWatchNotification
{
	public required WatchNotificationKind Kind { get; init; }
	public required Guid ParentItemId { get; init; }
	public required int Position { get; init; }
	public required MenuItem MenuItem { get; init; }
}

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(TextMenuItem), "Default")]
[JsonDerivedType(typeof(SubMenuMenuItem), "SubMenu")]
[JsonDerivedType(typeof(SeparatorMenuItem), "Separator")]
public abstract class MenuItem
{
	public Guid ItemId { get; }

	[JsonIgnore]
	public abstract MenuItemType Type { get; }

	protected MenuItem(Guid itemId) => ItemId = itemId;

	public virtual bool NonRecursiveEquals(MenuItem other) => Equals(other);

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is MenuItem other && Equals(other);

	public virtual bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type;

	public override int GetHashCode() => HashCode.Combine(ItemId, Type);
}

public class TextMenuItem : MenuItem
{
	public string Text { get; }

	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.Default;

	public TextMenuItem(Guid itemId, string text) : base(itemId)
	{
		Text = text;
	}

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is TextMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is TextMenuItem otherItem && Equals(otherItem);

	public virtual bool Equals(TextMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type && Text == other.Text;

	public override int GetHashCode() => HashCode.Combine(ItemId, Type, Text);
}

public sealed class SubMenuMenuItem : TextMenuItem
{
	public ImmutableArray<MenuItem> MenuItems { get; }

	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.SubMenu;

	public SubMenuMenuItem(Guid itemId, string text, ImmutableArray<MenuItem> menuItems) : base(itemId, text)
	{
		MenuItems = menuItems;
	}

	public override bool NonRecursiveEquals(MenuItem other) => ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && ItemId == otherItem.ItemId && Type == otherItem.Type && Text == otherItem.Text;

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is SubMenuMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && Equals(otherItem);

	public override bool Equals(TextMenuItem other)
		=> ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && Equals(otherItem);

	public bool Equals(SubMenuMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type && Text == other.Text && MenuItems.AsSpan().SequenceEqual(other.MenuItems.AsSpan());

	public override int GetHashCode() => HashCode.Combine(ItemId, Type, Text, MenuItems.Length);
}

public sealed class SeparatorMenuItem : MenuItem
{
	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.Separator;

	public SeparatorMenuItem(Guid itemId) : base(itemId)
	{
	}

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is SeparatorMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is SeparatorMenuItem otherItem && Equals(otherItem);

	public bool Equals(SeparatorMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type;

	public override int GetHashCode() => base.GetHashCode();
}
