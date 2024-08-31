namespace DeviceTools.Logitech.HidPlusPlus;

public readonly struct BatteryPowerState
{
	public BatteryPowerState(byte? batteryLevel, BatteryChargeStatus chargeStatus, BatteryExternalPowerStatus externalPowerStatus)
	{
		BatteryLevel = batteryLevel;
		ChargeStatus = chargeStatus;
		ExternalPowerStatus = externalPowerStatus;
	}

	public byte? BatteryLevel { get; }
	public BatteryChargeStatus ChargeStatus { get; }
	public BatteryExternalPowerStatus ExternalPowerStatus { get; }
}
