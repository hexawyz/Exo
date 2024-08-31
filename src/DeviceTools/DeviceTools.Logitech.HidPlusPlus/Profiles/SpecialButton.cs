namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

public enum SpecialButton : byte
{
	None = 0x00,
	TiltLeft = 0x01,
	TiltRight = 0x02,
	NextDpi = 0x03,
	PreviousDpi = 0x04,
	CycleDpi = 0x05,
	DefaultDpi = 0x06,
	ShiftDpi = 0x07,
	NextProfile = 0x08,
	PreviousProfile = 0x09,
	CycleProfile = 0x0A,
	GShift = 0x0B,

	BatteryIndicator = 0x0C,
	EnableProfile = 0x0D,
	PerformanceSwitch = 0x0E,
	Host = 0x0F,
	ScrollDown = 0x10,
	ScrollUp = 0x11,
}
