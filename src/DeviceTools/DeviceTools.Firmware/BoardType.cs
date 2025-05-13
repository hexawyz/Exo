namespace DeviceTools.Firmware;

public enum BoardType
{
	Unspecified = 0x00,

	Unknown = 0x01,
	Other = 0x02,
	ServerBlade = 0x03,
	ConnectivitySwitch = 0x04,
	SystemManagementModule = 0x05,
	ProcessorModule = 0x06,
	IoModule = 0x07,
	MemoryModule = 0x08,
	DaughterBoard = 0x09,
	Motherboard = 0x0A,
	ProcessorMemoryModule = 0x0B,
	ProcessorIoModule = 0x0C,
	InterconnectBoard = 0x0D,
}
