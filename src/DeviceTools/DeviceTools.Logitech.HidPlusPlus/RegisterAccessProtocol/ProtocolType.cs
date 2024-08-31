namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

public enum ProtocolType : byte
{
	Bluetooth = 0x01,
	TwentySevenMegaHertz = 0x02,
	Quad = 0x03,
	EQuadDj = 0x04, 
	DeviceFirmwareUpdateLight = 0x05,
	EQUadLite = 0x06,
	EQuadHighReportRate = 0x07,
	EQuadGamePad = 0x08,
}
