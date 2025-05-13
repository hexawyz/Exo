namespace DeviceTools.Firmware;

public enum SystemEnclosureState : byte
{
	Unspecified = 0x00,

	Other = 0x01,
	Unknown = 0x02,
	Safe = 0x03,
	Warning = 0x04,
	Critical = 0x05,
	NonRecoverable = 0x06,
}
