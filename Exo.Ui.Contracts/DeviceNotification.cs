using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceNotification
{
#nullable disable
	private DeviceNotification() { }
#nullable enable

	public DeviceNotification(DeviceNotificationKind notificationKind, DeviceInformation deviceInformation)
	{
		NotificationKind = notificationKind;
		DeviceInformation = deviceInformation;
	}

	[DataMember(Order = 1)]
	public DeviceNotificationKind NotificationKind { get; }
	[DataMember(Order = 2)]
	public DeviceInformation DeviceInformation { get; }
}
