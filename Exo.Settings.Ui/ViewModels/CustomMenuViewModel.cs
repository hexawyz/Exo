using System.Collections.ObjectModel;
using System.Windows.Input;
using Exo.Contracts.Ui;
using Exo.Ui;
using static System.Net.Mime.MediaTypeNames;

namespace Exo.Settings.Ui.ViewModels;

internal class CustomMenuViewModel : ChangeableBindableObject
{
	private readonly SubMenuMenuItemViewModel _rootMenu;
	private MenuItemViewModel? _selectedMenuItem;
	private readonly ObservableCollection<SubMenuMenuItemViewModel> _editedMenuHierarchy;
	private readonly ReadOnlyObservableCollection<SubMenuMenuItemViewModel> _readOnlyEditedMenuHierarchy;
	private readonly HashSet<Guid> _registeredGuids;

	private readonly Commands.AddTextItemCommand _addTextItemCommand;
	private readonly Commands.AddSeparatorItemCommand _addSeparatorItemCommand;
	private readonly Commands.AddSubMenuItemCommand _addSubMenuItemCommand;
	private readonly Commands.DeleteSelectedItemCommand _deleteSelectedItemCommand;
	private readonly Commands.NavigateToSubMenuCommand _navigateToSubMenuCommand;

	private event EventHandler? _canDeleteItemChanged;

	public CustomMenuViewModel()
	{
		_addTextItemCommand = new(this);
		_addSeparatorItemCommand = new(this);
		_addSubMenuItemCommand = new(this);
		_deleteSelectedItemCommand = new(this);
		_navigateToSubMenuCommand = new(this);

		_rootMenu = new(Constants.RootMenuItem, "Root", _navigateToSubMenuCommand);
		_registeredGuids = [Constants.RootMenuItem];
		_rootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 1"));
		_rootMenu.MenuItems.Add(new SeparatorMenuItemViewModel(Guid.NewGuid()));
		_rootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 2"));
		_editedMenuHierarchy = [_rootMenu];
		_readOnlyEditedMenuHierarchy = new(_editedMenuHierarchy);
	}

	private Guid NewGuid()
	{
		while (true)
		{
			var guid = Guid.NewGuid();

			if (_registeredGuids.Add(guid)) return guid;
		}
	}

	public override bool IsChanged => false;

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

	private int GetSelectedItemIndex() => SelectedMenuItem is { } item ? EditedMenu.MenuItems.IndexOf(item) : -1;

	private int GetItemInsertionIndex() => SelectedMenuItem is { } item ? EditedMenu.MenuItems.IndexOf(item) + 1 : EditedMenu.MenuItems.Count;

	private void AddMenuItem(MenuItemViewModel menuItem)
	{
		EditedMenu.MenuItems.Insert(GetItemInsertionIndex(), menuItem);
		SelectedMenuItem = menuItem;
	}

	private void AddTextItem(string text = "New Command Item") => AddMenuItem(new TextMenuMenuItemViewModel(NewGuid(), text));

	private void AddSubMenuItem(string text = "New Submenu") => AddMenuItem(new SubMenuMenuItemViewModel(NewGuid(), text, _navigateToSubMenuCommand));

	private void AddSeparatorItem() => AddMenuItem(new SeparatorMenuItemViewModel(NewGuid()));

	private void DeleteSelectedItem()
	{
		int index = GetSelectedItemIndex();
		if (index < 0) return;
		EditedMenu.MenuItems.RemoveAt(index);
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
}

public abstract class MenuItemViewModel : BindableObject
{
	public Guid ItemId { get; }
	public abstract MenuItemType ItemType { get; }

	protected MenuItemViewModel(Guid itemId) => ItemId = itemId;
}

public class TextMenuMenuItemViewModel : MenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.Default;

	private string _text;

	public string Text
	{
		get => _text;
		set => SetValue(ref _text, value);
	}

	public TextMenuMenuItemViewModel(Guid itemId, string text) : base(itemId)
	{
		_text = text;
	}
}

public sealed class SeparatorMenuItemViewModel : MenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.Separator;

	public SeparatorMenuItemViewModel(Guid itemId) : base(itemId)
	{
	}
}

public sealed class SubMenuMenuItemViewModel : TextMenuMenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.SubMenu;
	public ObservableCollection<MenuItemViewModel> MenuItems { get; }
	public ICommand NavigateToCommand { get; }

	public SubMenuMenuItemViewModel(Guid itemId, string text, ICommand navigateToCommand) : base(itemId, text)
	{
		MenuItems = [];
		NavigateToCommand = navigateToCommand;
	}
}
