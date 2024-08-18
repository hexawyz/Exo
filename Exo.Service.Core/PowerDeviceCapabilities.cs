namespace Exo.Service;

public enum PowerDeviceCapabilities : byte
{
	None = 0x00,
	HasBattery = 0x01,
	HasLowPowerBatteryThreshold = 0x02,
	HasIdleTimer = 0x04,
}
