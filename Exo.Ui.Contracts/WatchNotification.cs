using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class WatchNotification<TDetails>
	where TDetails : class
{
	[DataMember(Order = 1)]
	public required WatchNotificationKind NotificationKind { get; init; }
	[DataMember(Order = 2)]
	public required TDetails Details { get; init; }
}
