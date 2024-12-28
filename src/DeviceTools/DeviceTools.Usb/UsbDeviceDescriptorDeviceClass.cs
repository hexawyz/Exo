namespace DeviceTools.Usb;

public enum UsbDeviceDescriptorDeviceClass : byte
{
	NotSpecified = 0x00,
	Communications = 0x02,
	Hub = 0x09,
	Billboard = 0x11,
	Diagnostic = 0xDC,
	Miscellaneous = 0xEF,
	VendorSpecific = 0xFF,
}
