namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[Flags]
public enum KeyboardButtonModifiers : byte
{
	None = 0x00,
	Control = 0x01,
	Shift = 0x02,
}
