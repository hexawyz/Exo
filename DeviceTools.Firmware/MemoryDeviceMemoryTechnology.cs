namespace DeviceTools.Firmware;

public enum MemoryDeviceMemoryTechnology : byte
{
	Other = 0x01,
	Unknown = 0x02,
	DynamicRam = 0x03,
	NonVolatileDimmN = 0x04,
	NonVolatileDimmD = 0x05,
	NonVolatileDimmP = 0x06,
	IntelOptanePersistentMemory = 0x07,
}
