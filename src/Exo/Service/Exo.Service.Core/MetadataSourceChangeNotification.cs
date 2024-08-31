using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct MetadataSourceChangeNotification
{
	public WatchNotificationKind NotificationKind { get; }
	public ImmutableArray<MetadataSourceInformation> Sources { get; }

	public MetadataSourceChangeNotification(WatchNotificationKind notificationKind, ImmutableArray<MetadataSourceInformation> sources)
	{
		NotificationKind = notificationKind;
		Sources = sources;
	}
}
