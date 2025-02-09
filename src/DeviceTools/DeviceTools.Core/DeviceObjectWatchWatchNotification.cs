namespace DeviceTools;

public readonly struct DeviceObjectWatchWatchNotification(WatchNotificationKind kind, DeviceObjectInformation @object)
{
	public WatchNotificationKind Kind { get; } = kind;
	public DeviceObjectInformation Object { get; } = @object;
}
