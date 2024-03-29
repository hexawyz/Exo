using System.Runtime.Serialization;
using Exo.Contracts.Ui;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class WatchNotification<TDetails>
	where TDetails : class
{
	[DataMember(Order = 1)]
	public required WatchNotificationKind NotificationKind { get; init; }
	[DataMember(Order = 2)]
	public required TDetails Details { get; init; }
}
