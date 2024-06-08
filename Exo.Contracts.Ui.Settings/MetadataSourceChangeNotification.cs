using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MetadataSourceChangeNotification
{
	[DataMember(Order = 1)]
	public required WatchNotificationKind NotificationKind { get; init; }

	[DataMember(Order = 2)]
	public required MetadataArchiveCategory Category { get; init; }

	[DataMember(Order = 3)]
	public required string ArchivePath { get; init; }
}
