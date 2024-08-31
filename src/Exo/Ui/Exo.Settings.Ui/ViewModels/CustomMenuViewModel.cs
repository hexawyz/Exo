using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

// NB: The change model in this class is a bit complex, but like in the ret of the application, we want the "changed" state to be computed from updates sent by the backend.
// This means that from a logical POV, we have to maintain two trees of items in parallel.
// However, while maintaining two trees would be relatively easy, it would make computing changes somewhat expensive.
// So instead of two distinct tree, each sub-menu will have a list of original items and current items.
// As item identity is based on item ID, comparisons should be relatively, and IDs only need to be removed once an item is not referenced from anywhere.
internal class CustomMenuViewModel : ApplicableResettableBindableObject, IConnectedState, IDisposable
{
	private readonly SubMenuMenuItemViewModel _rootMenu;
	private MenuItemViewModel? _selectedMenuItem;
	private readonly ObservableCollection<SubMenuMenuItemViewModel> _editedMenuHierarchy;
	private readonly ReadOnlyObservableCollection<SubMenuMenuItemViewModel> _readOnlyEditedMenuHierarchy;
	private readonly Dictionary<Guid, MenuItemViewModel> _originalRegisteredGuids;
	private readonly Dictionary<Guid, MenuItemViewModel> _liveRegisteredGuids;
	private readonly SettingsServiceConnectionManager _connectionManager;
	private ISettingsCustomMenuService? _customMenuService;

	private readonly Commands.AddTextItemCommand _addTextItemCommand;
	private readonly Commands.AddSeparatorItemCommand _addSeparatorItemCommand;
	private readonly Commands.AddSubMenuItemCommand _addSubMenuItemCommand;
	private readonly Commands.DeleteSelectedItemCommand _deleteSelectedItemCommand;
	private readonly Commands.NavigateToSubMenuCommand _navigateToSubMenuCommand;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	private event EventHandler? _canDeleteItemChanged;

	public CustomMenuViewModel(SettingsServiceConnectionManager connectionManager)
	{
		_addTextItemCommand = new(this);
		_addSeparatorItemCommand = new(this);
		_addSubMenuItemCommand = new(this);
		_deleteSelectedItemCommand = new(this);
		_navigateToSubMenuCommand = new(this);

		_rootMenu = new(Constants.RootMenuItem, "Root", "Root", _navigateToSubMenuCommand);
		_originalRegisteredGuids = new()
		{
			{ _rootMenu.ItemId, _rootMenu }
		};
		_liveRegisteredGuids = new()
		{
			{ _rootMenu.ItemId, _rootMenu }
		};
		_editedMenuHierarchy = [_rootMenu];
		_readOnlyEditedMenuHierarchy = new(_editedMenuHierarchy);

		_connectionManager = connectionManager;
		_cancellationTokenSource = new();
		_stateRegistration = connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		cts.Cancel();
		_stateRegistration.Dispose();
	}

	public override bool IsChanged => true;

	public SubMenuMenuItemViewModel RootMenu => _rootMenu;

	public ReadOnlyObservableCollection<SubMenuMenuItemViewModel> EditedMenuHierarchy => _readOnlyEditedMenuHierarchy;

	public SubMenuMenuItemViewModel EditedMenu => _editedMenuHierarchy[^1];

	public MenuItemViewModel? SelectedMenuItem
	{
		get => _selectedMenuItem;
		set
		{
			bool hadText = SelectedMenuItemHasText;
			bool wasNull = _selectedMenuItem is null;
			if (SetValue(ref _selectedMenuItem, value))
			{
				if (SelectedMenuItemHasText != hadText)
				{
					NotifyPropertyChanged(nameof(SelectedMenuItemHasText));
				}
				if (value is null != wasNull)
				{
					_canDeleteItemChanged?.Invoke(_deleteSelectedItemCommand, EventArgs.Empty);
				}
			}
		}
	}

	public bool SelectedMenuItemHasText => _selectedMenuItem is TextMenuMenuItemViewModel;

	public ICommand AddTextItemCommand => _addTextItemCommand;
	public ICommand AddSeparatorItemCommand => _addSeparatorItemCommand;
	public ICommand AddSubMenuItemCommand => _addSubMenuItemCommand;
	public ICommand DeleteSelectedItemCommand => _deleteSelectedItemCommand;
	public ICommand NavigateToSubMenuCommand => _navigateToSubMenuCommand;

	private Guid NewGuid()
	{
		while (true)
		{
			var guid = Guid.NewGuid();

			if (!_originalRegisteredGuids.ContainsKey(guid) && !_liveRegisteredGuids.ContainsKey(guid)) return guid;
		}
	}

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource is not { } cts || cts.IsCancellationRequested) return;
		using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			var customMenuService = await _connectionManager.GetCustomMenuServiceAsync(cts2.Token);
			_customMenuService = customMenuService;
			await WatchMenuChangesAsync(customMenuService, cts2.Token);
		}
	}

	void IConnectedState.Reset()
	{
		_rootMenu.OriginalMenuItems.Clear();
		_rootMenu.MenuItems.Clear();
		_originalRegisteredGuids.Clear();
		_liveRegisteredGuids.Clear();
		_originalRegisteredGuids.Add(_rootMenu.ItemId, _rootMenu);
		_liveRegisteredGuids.Add(_rootMenu.ItemId, _rootMenu);
		while (_editedMenuHierarchy.Count > 1)
		{
			_editedMenuHierarchy.RemoveAt(_editedMenuHierarchy.Count - 1);
		}
	}

	private async Task WatchMenuChangesAsync(ISettingsCustomMenuService customMenuService, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in customMenuService.WatchMenuChangesAsync(cancellationToken))
			{
				SubMenuMenuItemViewModel parentMenu;
				bool isRoot = notification.ParentItemId == Constants.RootMenuItem;

				if (isRoot)
				{
					parentMenu = _rootMenu;
				}
				else
				{
					parentMenu = (SubMenuMenuItemViewModel)_originalRegisteredGuids[notification.ParentItemId];
				}

				_originalRegisteredGuids.TryGetValue(notification.ItemId, out var existingItem);

				int menuItemPosition = (int)notification.Position;

				switch (notification.Kind)
				{
				case WatchNotificationKind.Enumeration:
					if (notification.Position != parentMenu.OriginalMenuItems.Count) throw new InvalidOperationException("Initial enumeration: Menu item position out of range.");
					goto case WatchNotificationKind.Addition;
				case WatchNotificationKind.Addition:
					{
						if (notification.Position > parentMenu.OriginalMenuItems.Count) throw new InvalidOperationException("Addition: Menu item position out of range.");
						if (existingItem is not null) throw new InvalidOperationException("Addition: Duplicate item ID.");
						MenuItemViewModel? menuItem;
						if (_liveRegisteredGuids.TryGetValue(notification.ItemId, out menuItem))
						{
							if (!parentMenu.MenuItems.Contains(menuItem)) throw new InvalidOperationException("Addition: A live item with the same ID is already attached somewhere else.");
						}
						else
						{
							menuItem = notification.ItemType switch
							{
								MenuItemType.Default => new TextMenuMenuItemViewModel(notification.ItemId, notification.Text ?? string.Empty, notification.Text ?? string.Empty),
								MenuItemType.SubMenu => new SubMenuMenuItemViewModel(notification.ItemId, notification.Text ?? string.Empty, notification.Text ?? string.Empty, _navigateToSubMenuCommand),
								MenuItemType.Separator => new SeparatorMenuItemViewModel(notification.ItemId),
								_ => throw new InvalidOperationException("Unsupported item type."),
							};
						}
						parentMenu.OriginalMenuItems.Insert(menuItemPosition, menuItem);
						_originalRegisteredGuids.Add(menuItem.ItemId, menuItem);
						if (!_liveRegisteredGuids.ContainsKey(menuItem.ItemId) && parentMenu.MenuItems.Contains(menuItem))
						{
							_liveRegisteredGuids.Add(menuItem.ItemId, menuItem);
						}
					}
					break;
				case WatchNotificationKind.Removal:
					{
						if (notification.Position >= parentMenu.OriginalMenuItems.Count) throw new InvalidOperationException("Removal: Menu item position out of range.");
						if (existingItem is null) throw new InvalidOperationException("Removal: Menu item not found.");
						if (!ReferenceEquals(parentMenu.OriginalMenuItems[menuItemPosition], existingItem)) throw new InvalidOperationException("Removal: Item mismatch.");
						if (existingItem.ItemType == MenuItemType.SubMenu && _editedMenuHierarchy.IndexOf((SubMenuMenuItemViewModel)existingItem) is int hierarchyItemIndex and >= 0)
						{
							int lastIndex = _editedMenuHierarchy.Count - 1;
							do
							{
								_editedMenuHierarchy.RemoveAt(lastIndex);
							}
							while (hierarchyItemIndex >= --lastIndex);
							NavigateToSubMenu(_editedMenuHierarchy[lastIndex]);
						}
						else if (ReferenceEquals(SelectedMenuItem, existingItem))
						{
							SelectedMenuItem = null;
						}
						parentMenu.OriginalMenuItems.RemoveAt(menuItemPosition);
						if (!parentMenu.MenuItems.Contains(existingItem))
						{
							_liveRegisteredGuids.Remove(existingItem.ItemId);
						}
						break;
					}
				case WatchNotificationKind.Update:
					{
						if (notification.Position >= parentMenu.OriginalMenuItems.Count) throw new InvalidOperationException("Update: Menu item position out of range.");
						if (existingItem is null) throw new InvalidOperationException("Update: Menu item not found.");
						if (!ReferenceEquals(parentMenu.OriginalMenuItems[menuItemPosition], existingItem))
						{
							parentMenu.OriginalMenuItems.Move(parentMenu.OriginalMenuItems.IndexOf(existingItem), (int)notification.Position);
						}
						if (notification.Text is not null && existingItem is TextMenuMenuItemViewModel tmi)
						{
							tmi.OriginalText = notification.Text;
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

	private int GetSelectedItemIndex() => SelectedMenuItem is { } item ? EditedMenu.MenuItems.IndexOf(item) : -1;

	private int GetItemInsertionIndex() => SelectedMenuItem is { } item ? EditedMenu.MenuItems.IndexOf(item) + 1 : EditedMenu.MenuItems.Count;

	private void AddMenuItem(MenuItemViewModel menuItem)
	{
		EditedMenu.MenuItems.Insert(GetItemInsertionIndex(), menuItem);
		_liveRegisteredGuids.Add(menuItem.ItemId, menuItem);
		SelectedMenuItem = menuItem;
	}

	private void AddTextItem(string text = "New Command Item") => AddMenuItem(new TextMenuMenuItemViewModel(NewGuid(), null, text));

	private void AddSubMenuItem(string text = "New Submenu") => AddMenuItem(new SubMenuMenuItemViewModel(NewGuid(), null, text, _navigateToSubMenuCommand));

	private void AddSeparatorItem() => AddMenuItem(new SeparatorMenuItemViewModel(NewGuid()));

	private void DeleteSelectedItem()
	{
		int index = GetSelectedItemIndex();
		if (index < 0) return;
		var selectedMenuItem = SelectedMenuItem;
		EditedMenu.MenuItems.RemoveAt(index);
		_liveRegisteredGuids.Remove(selectedMenuItem!.ItemId);
		if (_originalRegisteredGuids.ContainsKey(selectedMenuItem.ItemId) && !EditedMenu.OriginalMenuItems.Contains(selectedMenuItem!))
		{
			_originalRegisteredGuids.Remove(selectedMenuItem.ItemId);
		}
		SelectedMenuItem = EditedMenu.MenuItems.Count > 0 ?
			EditedMenu.MenuItems[index == EditedMenu.MenuItems.Count ? EditedMenu.MenuItems.Count - 1 : index] :
			null;
	}

	private void NavigateToSubMenu(SubMenuMenuItemViewModel subMenu)
	{
		// A valid submenu would either be present in the hierarchy or as a direct child of the currently edited menu.
		if (_editedMenuHierarchy.IndexOf(subMenu) is int parentIndex and >= 0)
		{
			int lastIndex = _editedMenuHierarchy.Count - 1;
			if (lastIndex == parentIndex) return;
			SelectedMenuItem = null;
			do
			{
				_editedMenuHierarchy.RemoveAt(lastIndex--);
			}
			while (lastIndex > parentIndex);
			NotifyPropertyChanged(nameof(EditedMenu));
		}
		else if (EditedMenu.MenuItems.Contains(subMenu))
		{
			SelectedMenuItem = null;
			_editedMenuHierarchy.Add(subMenu);
			NotifyPropertyChanged(nameof(EditedMenu));
		}
	}

	private static class Commands
	{
		public sealed class AddTextItemCommand : ICommand
		{
			private readonly CustomMenuViewModel _owner;

			public AddTextItemCommand(CustomMenuViewModel owner) => _owner = owner;

			public void Execute(object? parameter) => _owner.AddTextItem();
			public bool CanExecute(object? parameter) => true;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}

		public sealed class AddSeparatorItemCommand : ICommand
		{
			private readonly CustomMenuViewModel _owner;

			public AddSeparatorItemCommand(CustomMenuViewModel owner) => _owner = owner;

			public void Execute(object? parameter) => _owner.AddSeparatorItem();
			public bool CanExecute(object? parameter) => true;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}

		public sealed class AddSubMenuItemCommand : ICommand
		{
			private readonly CustomMenuViewModel _owner;

			public AddSubMenuItemCommand(CustomMenuViewModel owner) => _owner = owner;

			public void Execute(object? parameter) => _owner.AddSubMenuItem();
			public bool CanExecute(object? parameter) => true;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}

		public sealed class DeleteSelectedItemCommand : ICommand
		{
			private readonly CustomMenuViewModel _owner;

			public DeleteSelectedItemCommand(CustomMenuViewModel owner)
			{
				_owner = owner;
			}

			public void Execute(object? parameter) => _owner.DeleteSelectedItem();
			public bool CanExecute(object? parameter) => _owner.SelectedMenuItem is not null;

			public event EventHandler? CanExecuteChanged
			{
				add => _owner._canDeleteItemChanged += value;
				remove => _owner._canDeleteItemChanged -= value;
			}
		}

		public sealed class NavigateToSubMenuCommand : ICommand
		{
			private readonly CustomMenuViewModel _owner;

			public NavigateToSubMenuCommand(CustomMenuViewModel owner) => _owner = owner;

			public void Execute(object? parameter) => _owner.NavigateToSubMenu((SubMenuMenuItemViewModel)parameter);
			public bool CanExecute(object? parameter) => true;

			public event EventHandler? CanExecuteChanged
			{
				add { }
				remove { }
			}
		}
	}

	protected override async Task ApplyChangesAsync(CancellationToken cancellationToken)
	{
		static MenuItemDefinition[] CreateArray(int count)
			=> count > 0 ? new MenuItemDefinition[count] : [];

		if (_customMenuService is null) return;

		var stack = new Stack<(SubMenuMenuItemViewModel SubMenu, int Index, MenuItemDefinition[] Definitions)> { };
		var currentMenu = _rootMenu;
		int index = 0;
		var definitions = CreateArray(currentMenu.MenuItems.Count);
		while (true)
		{
			if (index < currentMenu.MenuItems.Count)
			{
				var menuItem = currentMenu.MenuItems[index];
				switch (menuItem.ItemType)
				{
				case MenuItemType.Default:
					definitions[index++] = new() { ItemId = menuItem.ItemId, Type = MenuItemType.Default, Text = ((TextMenuMenuItemViewModel)menuItem).Text };
					break;
				case MenuItemType.SubMenu:
					stack.Push((currentMenu, index, definitions));
					currentMenu = (SubMenuMenuItemViewModel)menuItem;
					index = 0;
					definitions = CreateArray(currentMenu.MenuItems.Count);
					break;
				case MenuItemType.Separator:
					definitions[index++] = new() { ItemId = menuItem.ItemId, Type = MenuItemType.Separator };
					break;
				default:
					throw new InvalidOperationException();
				}
			}
			else if (stack.Count == 0)
			{
				break;
			}
			else
			{
				var definition = new MenuItemDefinition()
				{
					ItemId = currentMenu.ItemId,
					Type = MenuItemType.SubMenu,
					Text = currentMenu.Text,
					MenuItems = ImmutableCollectionsMarshal.AsImmutableArray(definitions)
				};

				(currentMenu, index, definitions) = stack.Pop();

				definitions[index++] = definition;
			}
		}

		await _customMenuService.UpdateMenuAsync(new MenuDefinition() { MenuItems = ImmutableCollectionsMarshal.AsImmutableArray(definitions) }, cancellationToken);
	}

	protected override void Reset() => throw new NotImplementedException();
}

internal abstract class MenuItemViewModel : ChangeableBindableObject
{
	public Guid ItemId { get; }
	public abstract MenuItemType ItemType { get; }

	protected MenuItemViewModel(Guid itemId) => ItemId = itemId;

	public virtual void Reset() { }
}

internal class TextMenuMenuItemViewModel : MenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.Default;

	private string? _originalText;
	private string _text;

	internal string? OriginalText
	{
		get => _originalText;
		set
		{
			if (value != _originalText)
			{
				if (value != _originalText)
				{
					bool wasChanged = IsChanged;
					if (_text == _originalText && value is not null)
					{
						_text = value!;
						NotifyPropertyChanged(ChangedProperty.Text);
					}
					_originalText = value;
					OnChangeStateChange(wasChanged);
				}
			}
		}
	}

	public string Text
	{
		get => _text;
		set => SetValue(ref _text, value, ChangedProperty.Text);
	}

	protected bool IsOriginalTextChanged => _originalText != _text;

	public override bool IsChanged => IsOriginalTextChanged;

	public TextMenuMenuItemViewModel(Guid itemId, string? originalText, string text) : base(itemId)
	{
		_originalText = originalText;
		_text = text;
	}

	public override void Reset()
	{
		if (OriginalText is not null)
		{
			Text = OriginalText;
		}
	}
}

internal sealed class SubMenuMenuItemViewModel : TextMenuMenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.SubMenu;
	internal ObservableCollection<MenuItemViewModel> OriginalMenuItems { get; }
	public ObservableCollection<MenuItemViewModel> MenuItems { get; }
	public ICommand NavigateToCommand { get; }
	private bool _haveItemsChanged;
	private bool _isUpdatingItems;

	internal bool HaveItemsChanged => _haveItemsChanged;

	public SubMenuMenuItemViewModel(Guid itemId, string? originalText, string text, ICommand navigateToCommand) : base(itemId, originalText, text)
	{
		OriginalMenuItems = [];
		MenuItems = [];
		NavigateToCommand = navigateToCommand;
		MenuItems.CollectionChanged += MenuItems_CollectionChanged;
		OriginalMenuItems.CollectionChanged += OriginalMenuItems_CollectionChanged;
	}

	private void OriginalMenuItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// If the item collections are currently synchronized, replicate the changes to the editable collection.
		if (!_haveItemsChanged)
		{
			_isUpdatingItems = true;
			try
			{
				// NB: ObservableCollection can only fire events in a limited set of conditions. The code below assumes that.
				switch (e.Action)
				{
				case NotifyCollectionChangedAction.Add:
					MenuItems.Insert(e.NewStartingIndex, (MenuItemViewModel)e.NewItems![0]!);
					break;
				case NotifyCollectionChangedAction.Remove:
					MenuItems.RemoveAt(e.OldStartingIndex);
					break;
				case NotifyCollectionChangedAction.Replace:
					MenuItems[e.NewStartingIndex] = (MenuItemViewModel)e.NewItems![0]!;
					break;
				case NotifyCollectionChangedAction.Move:
					MenuItems.Move(e.OldStartingIndex, e.NewStartingIndex);
					break;
				case NotifyCollectionChangedAction.Reset:
					MenuItems.Clear();
					break;
				}
			}
			finally
			{
				_isUpdatingItems = false;
			}
		}
	}

	private void MenuItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// Skip notifications occurring when items are being updated internally. (Reset or update notification from the backend)
		if (_isUpdatingItems) return;

		// Quickly recompute the changed status by looking at both item collections.
		// We could do "better", but it might not be worth it.
		bool hasChanged = OriginalMenuItems.Count != MenuItems.Count;
		if (!hasChanged)
		{
			for (int i = 0; i < MenuItems.Count; i++)
			{
				hasChanged |= !ReferenceEquals(MenuItems[i], OriginalMenuItems[i]);
			}
		}
		_haveItemsChanged = hasChanged;
	}

	public void RecursiveReset(HashSet<Guid> removedItemIds)
	{
		Reset();
	}
}

internal sealed class SeparatorMenuItemViewModel : MenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.Separator;

	public override bool IsChanged => false;

	public SeparatorMenuItemViewModel(Guid itemId) : base(itemId)
	{
	}
}
