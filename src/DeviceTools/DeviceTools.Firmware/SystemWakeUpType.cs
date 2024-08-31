namespace DeviceTools.Firmware;

public enum SystemWakeUpType : byte
{
	Reserved = 0x00,
	Other = 0x01,
	Unknown = 0x02,
	ApmTimer = 0x03,
	ModemRing = 0x04,
	LanRemote = 0x05,
	PowerSwitch = 0x06,
	PciPme = 0x07,
	AcPowerRestored = 0x08,
}
