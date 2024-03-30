using System.Diagnostics;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Overlay;

namespace Exo.Overlay;

internal sealed class NotifyIconService : IAsyncDisposable
{
	public static async ValueTask<NotifyIconService> CreateAsync(IOverlayCustomMenuService customMenuService)
	{
		// Setup the notification icon using interop code in order to get the native UI.
		// Sadly, the current state of notification icons is a total mess, and each app uses its own shitty implementation so there is no coherence at all.
		// Using native calls at least gives the basic look&feel, but then we're still out of style…
		var window = await NotificationWindow.GetOrCreateAsync().ConfigureAwait(false);
		await window.SwitchTo();
		return new NotifyIconService(window, customMenuService);
	}

	private readonly IOverlayCustomMenuService _customMenuService;

	private readonly NotificationWindow _window;
	private readonly NotifyIcon _icon;
	private readonly Dictionary<Guid, MenuItem> _customMenuItems;
	private readonly int _rootFirstCustomMenuItemIndex;
	private int _rootCustomMenuItemCount;
	private readonly EventHandler _customMenuItemEventHandler;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _watchTask;

	public NotifyIconService(NotificationWindow window, IOverlayCustomMenuService customMenuService)
	{
		_customMenuService = customMenuService;
		_window = window;
		_icon = window.CreateNotifyIcon(0, 32512, "Exo");
		var menu = _icon.ContextMenu;
		var settingsMenuItem = new TextMenuItem("&Settings…");
		settingsMenuItem.Click += OnSettingsMenuItemClick;
		settingsMenuItem.IsEnabled = App.SettingsUiExecutablePath is not null;
		var exitMenuItem = new TextMenuItem("E&xit");
		exitMenuItem.Click += OnExitMenuItemClick;
		menu.MenuItems.Add(settingsMenuItem);
		menu.MenuItems.Add(new SeparatorMenuItem());
		_rootFirstCustomMenuItemIndex = menu.MenuItems.Count;
		menu.MenuItems.Add(exitMenuItem);
		_customMenuItems = new();
		_customMenuItemEventHandler = new EventHandler(OnCustomMenuItemClick);
		_cancellationTokenSource = new();
		_watchTask = WatchMenuChangesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		cts.Cancel();

		await _window.SwitchTo();

		_icon.Dispose();

		await _watchTask.ConfigureAwait(false);

		cts.Dispose();
	}

	private async Task WatchMenuChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _window.SwitchTo();

			await foreach (var notification in _customMenuService.WatchMenuChangesAsync(cancellationToken))
			{
				PopupMenu parentMenu;
				int firstCustomMenuIndex;
				int customMenuItemCount;
				bool isRoot = notification.ParentItemId == Constants.RootMenuItem;

				if (isRoot)
				{
					parentMenu = _icon.ContextMenu;
					firstCustomMenuIndex = _rootFirstCustomMenuItemIndex;
					customMenuItemCount = _rootCustomMenuItemCount;
				}
				else
				{
					parentMenu = ((SubMenuMenuItem)_customMenuItems[notification.ParentItemId]).SubMenu;
					firstCustomMenuIndex = 0;
					customMenuItemCount = parentMenu.MenuItems.Count;
				}

				_customMenuItems.TryGetValue(notification.ItemId, out var existingItem);

				int menuItemPosition = firstCustomMenuIndex + (int)notification.Position;

				switch (notification.Kind)
				{
				case WatchNotificationKind.Enumeration:
					if (notification.Position != customMenuItemCount) throw new InvalidOperationException("Initial enumeration: Menu item position out of range.");
					goto case WatchNotificationKind.Addition;
				case WatchNotificationKind.Addition:
					{
						if (notification.Position > customMenuItemCount) throw new InvalidOperationException("Addition: Menu item position out of range.");
						if (existingItem is not null) throw new InvalidOperationException("Addition: Duplicate item ID.");
						MenuItem menuItem = notification.ItemType switch
						{
							MenuItemType.Default => CreateTextMenuItem(notification.Text!),
							MenuItemType.SubMenu => new SubMenuMenuItem(notification.Text!, _window.CreatePopupMenu()),
							MenuItemType.Separator => new SeparatorMenuItem(),
							_ => throw new InvalidOperationException("Unsupported item type."),
						};
						menuItem.Tag = notification.ItemId;
						parentMenu.MenuItems.Insert(menuItemPosition, menuItem);
						if (isRoot)
						{
							if (_rootCustomMenuItemCount++ == 0)
							{
								// After the first custom item is added, we insert a separator before the exit option.
								parentMenu.MenuItems.Insert(firstCustomMenuIndex + _rootCustomMenuItemCount, new SeparatorMenuItem());
							}
						}
						_customMenuItems.Add(notification.ItemId, menuItem);
					}
					break;
				case WatchNotificationKind.Removal:
					{
						if (notification.Position >= customMenuItemCount) throw new InvalidOperationException("Removal: Menu item position out of range.");
						if (existingItem is null) throw new InvalidOperationException("Removal: Menu item not found.");
						if (!ReferenceEquals(parentMenu.MenuItems[menuItemPosition], existingItem)) throw new InvalidOperationException("Removal: Item mismatch.");
						parentMenu.MenuItems.RemoveAt(menuItemPosition);
						if (isRoot)
						{
							if (--_rootCustomMenuItemCount == 0)
							{
								// After the last root custom item is removed, remove the extra separator before the exit option.
								parentMenu.MenuItems.RemoveAt(firstCustomMenuIndex + _rootCustomMenuItemCount);
							}
						}
						_customMenuItems.Remove(notification.ItemId);
						break;
					}
				case WatchNotificationKind.Update:
					{
						if (notification.Position >= customMenuItemCount) throw new InvalidOperationException("Update: Menu item position out of range.");
						if (existingItem is null) throw new InvalidOperationException("Update: Menu item not found.");
						if (!ReferenceEquals(parentMenu.MenuItems[menuItemPosition], existingItem))
						{
							parentMenu.MenuItems.Move(existingItem.Index, (int)notification.Position);
						}
						if (notification.Text is not null && existingItem is BaseTextMenuItem tmi && tmi.Text != notification.Text)
						{
							tmi.Text = notification.Text!;
						}
					}
					break;
				}
			}
		}
		catch (Exception) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private TextMenuItem CreateTextMenuItem(string text)
	{
		var menuItem = new TextMenuItem(text);
		menuItem.Click += _customMenuItemEventHandler;
		return menuItem;
	}

	private static bool HasId(MenuItem menuItem, Guid itemId) => menuItem.Tag is Guid guid && guid == itemId;

	private void GetMenuItem()
	{
	}

	private async void OnCustomMenuItemClick(object? sender, EventArgs e)
	{
		await _customMenuService.InvokeMenuItemAsync(new MenuItemReference { Id = (Guid)((MenuItem)sender!).Tag! }, default).ConfigureAwait(false);
	}

	private void OnSettingsMenuItemClick(object? sender, EventArgs e)
	{
		if (App.SettingsUiExecutablePath is { Length: > 0 } path)
		{
			Process.Start(path);
		}
	}

	private async void OnExitMenuItemClick(object? sender, EventArgs e)
	{
		await DisposeAsync();
		await App.Current.RequestShutdown();
	}
}
