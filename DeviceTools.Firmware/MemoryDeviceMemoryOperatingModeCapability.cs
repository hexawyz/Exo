namespace DeviceTools.Firmware;

[Flags]
public enum MemoryDeviceMemoryOperatingModeCapability : ushort
{
	Other = 0x0002,
	Unknown = 0x0004,
	VolatileMemory = 0x0008,
	ByteAccessiblePersistentMemory = 0x0010,
	BlockAccessiblePersistentMemory = 0x0020,
}
