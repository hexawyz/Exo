using System.Collections.ObjectModel;
using Exo.Contracts.Ui;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal class CustomMenuViewModel : ChangeableBindableObject
{
	private readonly SubMenuMenuItemViewModel _rootMenu;
	private MenuItemViewModel? _selectedMenuItem;
	private readonly ObservableCollection<SubMenuMenuItemViewModel> _editedMenuHierarchy;
	private readonly ReadOnlyObservableCollection<SubMenuMenuItemViewModel> _readOnlyEditedMenuHierarchy;

	public CustomMenuViewModel()
	{
		_rootMenu = new(Constants.RootMenuItem, "Root");
		_rootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 1"));
		_rootMenu.MenuItems.Add(new SeparatorMenuItemViewModel(Guid.NewGuid()));
		_rootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 2"));
		_editedMenuHierarchy = [_rootMenu];
		_readOnlyEditedMenuHierarchy = new(_editedMenuHierarchy);
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
			if (SetValue(ref _selectedMenuItem, value))
			{
				if (SelectedMenuItemHasText != hadText)
				{
					NotifyPropertyChanged(nameof(SelectedMenuItemHasText));
				}
			}
		}
	}

	public bool SelectedMenuItemHasText => _selectedMenuItem is TextMenuMenuItemViewModel;
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

	public SubMenuMenuItemViewModel(Guid itemId, string text) : base(itemId, text) => MenuItems = [];
}
