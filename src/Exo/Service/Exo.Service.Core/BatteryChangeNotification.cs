using Exo.Features;

namespace Exo.Service;

public readonly struct BatteryChangeNotification(Guid deviceId, float? level, BatteryStatus batteryStatus, ExternalPowerStatus externalPowerStatus)
{
	public Guid DeviceId { get; } = deviceId;
	public float? Level { get; } = level;
	public BatteryStatus BatteryStatus { get; } = batteryStatus;
	public ExternalPowerStatus ExternalPowerStatus { get; } = externalPowerStatus;
}
