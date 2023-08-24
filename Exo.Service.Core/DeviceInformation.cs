namespace Exo.Service;

public sealed class DeviceInformation : IEquatable<DeviceInformation?>
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

	public override bool Equals(object? obj) => Equals(obj as DeviceInformation);

	public bool Equals(DeviceInformation? other)
		=> other is not null && Id.Equals(other.Id) &&
			FriendlyName == other.FriendlyName &&
			Category == other.Category &&
			FeatureTypes.SequenceEqual(other.FeatureTypes) &&
			EqualityComparer<Type>.Default.Equals(DriverType, other.DriverType);

	public override int GetHashCode() => HashCode.Combine(Id, FriendlyName, Category, FeatureTypes, DriverType);

	public static bool operator ==(DeviceInformation? left, DeviceInformation? right) => EqualityComparer<DeviceInformation>.Default.Equals(left, right);
	public static bool operator !=(DeviceInformation? left, DeviceInformation? right) => !(left == right);
}
