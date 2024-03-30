using System.Collections.ObjectModel;
using Exo.Contracts.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal class CustomMenuViewModel : ChangeableBindableObject
{
	public override bool IsChanged => false;

	public SubMenuMenuItemViewModel RootMenu { get; } = new(Constants.RootMenuItem, "Root");

	public CustomMenuViewModel()
	{
		RootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 1"));
		RootMenu.MenuItems.Add(new SeparatorMenuItemViewModel(Guid.NewGuid()));
		RootMenu.MenuItems.Add(new TextMenuMenuItemViewModel(Guid.NewGuid(), "Test 2"));
	}
}

public abstract class MenuItemViewModel
{
	public Guid ItemId { get; }
	public abstract MenuItemType ItemType { get; }

	protected MenuItemViewModel(Guid itemId) => ItemId = itemId;
}

public class TextMenuMenuItemViewModel : MenuItemViewModel
{
	public override MenuItemType ItemType => MenuItemType.Default;
	public string Text { get; set; }

	public TextMenuMenuItemViewModel(Guid itemId, string text) : base(itemId)
	{
		Text = text;
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
