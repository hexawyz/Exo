namespace DeviceTools.Firmware;

[Flags]
public enum ProcessorCharacteristics : ushort
{
	Unknown = 0x0002,
	Is64BitCapable = 0x0004,
	MultiCore = 0x0008,
	HardwareThread = 0x0010,
	ExecuteProtection = 0x0020,
	EnhancedVirtualization = 0x0040,
	PowerAndPerformanceControl = 0x0080,
	Is128BitCapable = 0x0100,
	Arm64SystemOnChip = 0x0200,
}
