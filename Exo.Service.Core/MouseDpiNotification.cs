using System.Collections.Immutable;

namespace Exo.Service;

public readonly record struct MouseDpiNotification
{
	public MouseDpiNotification(WatchNotificationKind notificationKind, Guid deviceId, MouseDpiStatus newValue, MouseDpiStatus oldValue, ImmutableArray<DotsPerInch> presets)
	{
		NotificationKind = notificationKind;
		DeviceId = deviceId;
		NewValue = newValue;
		OldValue = oldValue;
		Presets = presets;
	}

	public WatchNotificationKind NotificationKind { get; }
	public Guid DeviceId { get; }
	public MouseDpiStatus NewValue { get; }
	public MouseDpiStatus OldValue { get; }
	public ImmutableArray<DotsPerInch> Presets { get; }
}
