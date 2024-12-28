namespace DeviceTools.Usb;

public enum UsbDescriptorType : byte
{
	Device = 0x01,
	Configuration = 0x02,
	String = 0x03,
	Interface = 0x04,
	Endpoint = 0x05,
	DeviceQualifier = 0x06,
	OtherSpeedConfiguration = 0x07,
	InterfacePower = 0x08,
	OnTheGo = 0x09,
	Debug = 0x0a,
	InterfaceAssociation = 0x0b,
	BinaryDeviceObjectStore = 0x0f,
	DeviceCapability = 0x10,
	Hub20 = 0x29,
	Hub30 = 0x2a,
	SuperSpeedEndpointCompanion = 0x30,
	SuperSpeedPlusIsochronousEndpointCompanion = 0x31,
}
