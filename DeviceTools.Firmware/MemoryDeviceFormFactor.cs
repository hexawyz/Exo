namespace DeviceTools.Firmware;

public enum MemoryDeviceFormFactor : byte
{
	Other = 0x01,
	Unknown = 0x02,
	Simm = 0x03,
	Sip = 0x04,
	Chip = 0x05,
	Dip = 0x06,
	Zip = 0x07,
	ProprietaryCard = 0x08,
	Dimm = 0x09,
	Tsop = 0x0A,
	RowOfChips = 0x0B,
	Rimm = 0x0C,
	Sodimm = 0x0D,
	Srimm = 0x0E,
	FbDimm = 0x0F,
	Die = 0x10,
}
