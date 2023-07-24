using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceInformation
{
#nullable disable
	private DeviceInformation() { }
#nullable enable

	public DeviceInformation(string uniqueId, string friendlyName, DeviceCategory category, string driverTypeName, string[] featureTypeNames)
	{
		UniqueId = uniqueId;
		FriendlyName = friendlyName;
		Category = category;
		DriverTypeName = driverTypeName;
		FeatureTypeNames = featureTypeNames ?? Array.Empty<string>();
	}

	[DataMember(Order = 1, IsRequired = true)]
	public string UniqueId { get; }
	[DataMember(Order = 2, IsRequired = true)]
	public string FriendlyName { get; }
	[DataMember(Order = 3, IsRequired = true)]
	public DeviceCategory Category { get; }
	[DataMember(Order = 4, IsRequired = true)]
	public string DriverTypeName { get; }
	[DataMember(Order = 5)]
	public string[] FeatureTypeNames { get; }
}
