namespace Exo.Service;

public readonly record struct ChangeWatchNotification<TKey, TValue>
{
	public WatchNotificationKind NotificationKind { get; }
	public TKey Key { get; }
	public TValue? NewValue { get; }
	public TValue? OldValue { get; }

	public ChangeWatchNotification(WatchNotificationKind notificationKind, TKey key, TValue? newValue, TValue? oldValue)
	{
		NotificationKind = notificationKind;
		Key = key;
		NewValue = newValue;
		OldValue = oldValue;
	}
}
