namespace Exo.Service;

public enum MenuItemType : byte
{
	// A standard command item.
	Default = 0,
	// A submenu item.
	SubMenu = 1,

	// We probably want to enable more options.
	//[EnumMember]
	//ToggleCommand = 2,
	//[EnumMember]
	//ChoiceSubMenu = 3,

	// A separator item.
	Separator = 255,
}
