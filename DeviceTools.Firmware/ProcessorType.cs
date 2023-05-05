namespace DeviceTools.Firmware;

public enum ProcessorType : byte
{
	Other = 0x01,
	Unknown = 0x02,
	CentralProcessor = 0x03,
	MathProcessor = 0x04,
	DspProcessor = 0x05,
	VideoProcessor = 0x06,
}
