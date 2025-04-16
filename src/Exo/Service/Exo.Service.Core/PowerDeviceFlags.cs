namespace Exo.Service;

[Flags]
internal enum PowerDeviceFlags : byte
{
	None = 0b00000000,

	HasBattery = 0b00000001,
	HasLowPowerBatteryThreshold = 0b00000010,
	HasIdleTimer = 0b00000100,
	HasWirelessBrightness = 0b00001000,

	IsConnected = 0b10000000,
}
