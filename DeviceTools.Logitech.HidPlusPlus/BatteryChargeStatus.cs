namespace DeviceTools.Logitech.HidPlusPlus;

public enum BatteryChargeStatus : byte
{
	Discharging = 0,
	Charging = 1,
	ChargingNearlyComplete = 2,
	ChargingComplete = 3,

	ChargingError = 4,
	InvalidBatteryType = 5,
	BatteryTooHot = 6,
}
