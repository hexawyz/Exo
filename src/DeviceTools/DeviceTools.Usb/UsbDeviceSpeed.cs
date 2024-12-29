namespace DeviceTools.Usb;

public enum UsbDeviceSpeed : byte
{
	UsbLowSpeed = 0b00,
	UsbFullSpeed = 0b01,
	UsbHighSpeed = 0b10,
	UsbSuperSpeed = 0b11,
}
