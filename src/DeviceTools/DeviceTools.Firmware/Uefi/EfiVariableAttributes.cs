namespace DeviceTools.Firmware.Uefi;

[Flags]
public enum EfiVariableAttributes : uint
{
	NonVolatile = 0x00000001,
	BootServiceAccess = 0x00000002,
	RuntimeAccess = 0x00000004,
	HardwareErrorRecord = 0x00000008,
	AuthenticatedWriteAccess = 0x00000010,
	TimeBasedAuthenticatedWriteAccess = 0x00000020,
	AppendWrite = 0x00000040,
	EnhancedAuthenticatedAccess = 0x00000080,
}
