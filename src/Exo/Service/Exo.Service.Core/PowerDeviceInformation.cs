namespace Exo.Service;

internal readonly struct PowerDeviceInformation(Guid deviceId, PowerDeviceFlags flags, TimeSpan minimumIdleTime, TimeSpan maximumIdleTime, byte minimumBrightness, byte maximumBrightness)
{
	public Guid DeviceId { get; } = deviceId;
	public PowerDeviceFlags Flags { get; } = flags;
	public TimeSpan MinimumIdleTime { get; } = minimumIdleTime;
	public TimeSpan MaximumIdleTime { get; } = maximumIdleTime;
	public byte MinimumBrightness { get; } = minimumBrightness;
	public byte MaximumBrightness { get; } = maximumBrightness;
}
