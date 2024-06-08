namespace Exo.Service;

public readonly struct MetadataSourceChangeNotification
{
	public WatchNotificationKind NotificationKind { get; }
	public MetadataArchiveCategory Category { get; }
	public string ArchivePath { get; }

	public MetadataSourceChangeNotification(WatchNotificationKind notificationKind, MetadataArchiveCategory category, string archivePath)
	{
		NotificationKind = notificationKind;
		Category = category;
		ArchivePath = archivePath;
	}
}
