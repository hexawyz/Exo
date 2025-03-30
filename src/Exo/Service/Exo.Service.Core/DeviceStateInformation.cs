using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Service;

public sealed class DeviceStateInformation : IEquatable<DeviceStateInformation?>
{
	public Guid Id { get; }
	public string FriendlyName { get; }
	public string UserFriendlyName { get; }
	public DeviceCategory Category { get; }
	public HashSet<Guid> FeatureIds { get; }
	public ImmutableArray<DeviceId> DeviceIds { get; }
	public int? MainDeviceIdIndex { get; }
	public string? SerialNumber { get; }
	public bool IsAvailable { get; }

	public DeviceStateInformation
	(
		Guid id,
		string friendlyName,
		string userFriendlyName,
		DeviceCategory category,
		HashSet<Guid> featureIds,
		ImmutableArray<DeviceId> deviceIds,
		int? mainDeviceIdIndex,
		string? serialNumber,
		bool isAvailable
	)
	{
		Id = id;
		FriendlyName = friendlyName;
		UserFriendlyName = userFriendlyName;
		Category = category;
		FeatureIds = featureIds;
		DeviceIds = deviceIds;
		MainDeviceIdIndex = mainDeviceIdIndex;
		SerialNumber = serialNumber;
		IsAvailable = isAvailable;
	}

	public override bool Equals(object? obj) => Equals(obj as DeviceStateInformation);

	public bool Equals(DeviceStateInformation? other)
		=> other is not null &&
		Id.Equals(other.Id) &&
		FriendlyName == other.FriendlyName &&
		UserFriendlyName == other.UserFriendlyName &&
		Category == other.Category &&
		FeatureIds.SetEquals(other.FeatureIds) &&
		DeviceIds.SequenceEqual(other.DeviceIds) &&
		MainDeviceIdIndex == other.MainDeviceIdIndex &&
		SerialNumber == other.SerialNumber &&
		IsAvailable == other.IsAvailable;

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Id);
		hash.Add(FriendlyName);
		hash.Add(UserFriendlyName);
		hash.Add(Category);
		hash.Add(FeatureIds);
		hash.Add(DeviceIds.Length);
		hash.Add(MainDeviceIdIndex);
		hash.Add(SerialNumber);
		hash.Add(IsAvailable);
		return hash.ToHashCode();
	}

	public static bool operator ==(DeviceStateInformation? left, DeviceStateInformation? right) => EqualityComparer<DeviceStateInformation>.Default.Equals(left, right);
	public static bool operator !=(DeviceStateInformation? left, DeviceStateInformation? right) => !(left == right);
}
