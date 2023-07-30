using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class WatchNotification<TDetails>
	where TDetails : class
{
#nullable disable
	private WatchNotification() { }
#nullable enable

	public WatchNotification(WatchNotificationKind notificationKind, TDetails details)
	{
		NotificationKind = notificationKind;
		Details = details;
	}

	[DataMember(Order = 1)]
	public WatchNotificationKind NotificationKind { get; }
	[DataMember(Order = 2)]
	public TDetails Details { get; }
}
