namespace DeviceTools.Logitech.HidPlusPlus;

[Flags]
public enum BatteryExternalPowerStatus : byte
{
	None = 0,
	IsConnected = 1,
	IsChargingBelowOptimalSpeed = 2,
}
