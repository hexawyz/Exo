using System;

namespace Exo.Service;

public sealed class DeviceInformation
{
	public DeviceInformation(Guid uniqueId, string friendlyName, DeviceCategory category, Type[] featureTypes, Type driverType)
	{
		Id = uniqueId;
		FriendlyName = friendlyName;
		Category = category;
		FeatureTypes = featureTypes;
		DriverType = driverType;
	}

	public Guid Id { get; }
	public string FriendlyName { get; }
	public DeviceCategory Category { get; }
	public Type[] FeatureTypes { get; }
	public Type DriverType { get; }
}
