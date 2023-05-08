namespace DeviceTools.Firmware;

[Flags]
public enum MemoryDeviceTypeDetail : ushort
{
	Other = 0x0002,
	Unknown = 0x0004,
	FastPaged = 0x0008,
	StaticColumn = 0x0010,
	PseudoStatic = 0x0020,
	RamBus = 0x0040,
	Synchronous = 0x0080,
	Cmos = 0x0100,
	Edo = 0x0200,
	WindowDynamicRam = 0x0400,
	CacheDynamicRam = 0x0800,
	NonVolatile = 0x1000,
	Registered = 0x2000,
	Buffered = 0x2000,
	Unregistered = 0x4000,
	Unbuffered = 0x4000,
	LoadReducedDimm = 0x8000,
}
