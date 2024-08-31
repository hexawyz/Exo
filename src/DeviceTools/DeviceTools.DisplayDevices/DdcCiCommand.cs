namespace DeviceTools.DisplayDevices;

public enum DdcCiCommand : byte
{
	VcpRequest = 0x01,
	VcpReply = 0x02,
	VcpSet = 0x03,
	TimingReply = 0x06,
	TimingRequest = 0x07,
	VcpReset = 0x09,
	SaveCurrentSettings = 0x0C,
	DisplaySelfTestReply = 0xA1,
	DisplaySelfTestRequest = 0xB1,
	IdentificationReply = 0xE1,
	TableReadRequest = 0xE2,
	CapabilitiesReply = 0xE3,
	TableReadReply = 0xE4,
	TableWrite = 0xE7,
	IdentificationRequest = 0xF1,
	CapabilitiesRequest = 0xF3,
	EnableApplicationReport = 0xF5,
}
