namespace Exo.Service;

internal readonly struct MousePollingFrequencyNotification
{
	public required Guid DeviceId { get; init; }
	public required ushort PollingFrequency { get; init; }
}
