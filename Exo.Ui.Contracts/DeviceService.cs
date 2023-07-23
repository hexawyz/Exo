using System.Runtime.Serialization;
using System.ServiceModel;

namespace Exo.Ui.Contracts;

[DataContract]
public enum DeviceNotificationKind
{
	Enumeration = 0,
	Arrival = 1,
	Removal = 2,
	Update = 3,
}

[DataContract]
public sealed class DeviceNotification
{
	private DeviceNotification() { }

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

[DataContract]
public sealed class DeviceInformation
{
	private DeviceInformation() { }

	public DeviceInformation(string uniqueId, string friendlyName, string driverTypeName, string[] featureTypeNames)
	{
		UniqueId = uniqueId;
		FriendlyName = friendlyName;
		DriverTypeName = driverTypeName;
		FeatureTypeNames = featureTypeNames ?? Array.Empty<string>();
	}

	[DataMember(Order = 1, IsRequired = true)]
	public string UniqueId { get; }
	[DataMember(Order = 2, IsRequired = true)]
	public string FriendlyName { get; }
	[DataMember(Order = 3, IsRequired = true)]
	public string DriverTypeName { get; }
	[DataMember(Order = 5)]
	public string[] FeatureTypeNames { get; }
}

[ServiceContract]
public interface IDeviceService
{
	[OperationContract]
	IAsyncEnumerable<DeviceNotification> GetDevicesAsync(CancellationToken cancellationToken);
}
