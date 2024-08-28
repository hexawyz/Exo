namespace DeviceTools.Logitech.HidPlusPlus;

[Flags]
public enum ReportIntervals : byte
{
	None = 0x00,
	Delay1ms = 0x01,
	Delay2ms = 0x02,
	Delay3ms = 0x04,
	Delay4ms = 0x08,
	Delay5ms = 0x10,
	Delay6ms = 0x20,
	Delay7ms = 0x40,
	Delay8ms = 0x80,
}
