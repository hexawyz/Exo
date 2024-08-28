namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

public enum ButtonType : byte
{
	Macro = 0x00,
	Hid = 0x80,
	Special = 0x90,
	Disabled = 0xFF,
}
