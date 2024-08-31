namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[Flags]
public enum MouseButtons : ushort
{
	LeftButton = 0x0001,
	RightButton = 0x0002,
	MiddleButton = 0x0004,
	Button4 = 0x0008,
	Button5 = 0x0010,
}
