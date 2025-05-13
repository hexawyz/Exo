namespace DeviceTools.Firmware;

public enum SystemEnclosureSecurityStatus : byte
{
	Unspecified = 0x00,

	Other = 0x01,
	Unknown = 0x02,
	None = 0x03,
	ExternalInterfaceLockedOut = 0x04,
	ExternalInterfaceEnabled = 0x05,
}
