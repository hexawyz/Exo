namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

public enum DeviceType : byte
{
	Unknown = 0x00,
	Keyboard = 0x01,
	Mouse = 0x02,
	Numpad = 0x03,
	Presenter = 0x04,

	Trackball = 0x08,
	Touchpad = 0x09,
}
