namespace DeviceTools.Usb;

public enum UsbDeviceClass : byte
{
	NotSpecified = 0x00,
	Audio = 0x01,
	Communications = 0x02,
	HumanInterface = 0x03,
	Physical = 0x05,
	Imaging = 0x06,
	Printer = 0x07,
	MassStorage = 0x08,
	Hub = 0x09,
	CommunicationsData = 0x0A,
	SmartCard = 0x0B,
	ContentSecurity = 0x0D,
	Video = 0x0E,
	PersonalHealthcare = 0x0F,
	AudioVideo = 0x10,
	Billboard = 0x11,
	UsbTypeCBridge = 0x12,
	UsbBulkDisplayProtocol = 0x13,
	ManagementComponentTransportProtocol = 0x14,
	I3c = 0x3C,
	Diagnostic = 0xDC,
	WirelessController = 0xE0,
	Miscellaneous = 0xEF,
	ApplicationSpecific = 0xFE,
	VendorSpecific = 0xFF,
}
