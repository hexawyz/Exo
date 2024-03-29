using System.Runtime.Serialization;

namespace Exo.Core.Contracts;

[DataContract]
public enum MenuItemType : byte
{
	// A standard command item.
	[EnumMember]
	Default = 0,
	// A submenu item.
	[EnumMember]
	SubMenu = 1,

	// We probably want to enable more options.
	//[EnumMember]
	//ToggleCommand = 2,
	//[EnumMember]
	//ChoiceSubMenu = 3,

	// A separator item.
	[EnumMember]
	Separator = 255,
}
