namespace Exo.Service;

internal readonly struct PowerDeviceLowPowerBatteryThresholdNotification
{
	public required Guid DeviceId { get; init; }
	public required Half BatteryThreshold { get; init; }
}
