namespace Exo.Service;

internal readonly struct PowerDeviceIdleSleepTimerNotification
{
	public required Guid DeviceId { get; init; }
	public required TimeSpan IdleTime { get; init; }
}
