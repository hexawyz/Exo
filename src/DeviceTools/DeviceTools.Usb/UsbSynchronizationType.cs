namespace DeviceTools.Usb;

public enum UsbSynchronizationType : byte
{
	None = 0b00,
	Asynchronous = 0b01,
	Adaptive = 0b10,
	Synchronous = 0b11,
}
