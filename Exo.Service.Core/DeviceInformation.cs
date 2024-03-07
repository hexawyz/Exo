using System.Collections.Immutable;
using System.Runtime.Serialization;
using DeviceTools;

namespace Exo.Service;

[DataContract]
[TypeId(0xF9477332, 0x3B69, 0x4CF9, 0xBA, 0x73, 0xFA, 0xA4, 0xA1, 0xD8, 0xBE, 0x21)]
public sealed class DeviceInformation : IEquatable<DeviceInformation?>
{
	public DeviceInformation(string friendlyName, DeviceCategory category, HashSet<Guid> featureIds, ImmutableArray<DeviceId> deviceIds, int? mainDeviceIdIndex, string? serialNumber)
	{
		if (featureIds is null) throw new ArgumentNullException(nameof(featureIds));
		FriendlyName = friendlyName;
		Category = category;
		FeatureIds = featureIds;
		DeviceIds = deviceIds.IsDefault ? [] : deviceIds;
		MainDeviceIdIndex = mainDeviceIdIndex;
		SerialNumber = serialNumber;
	}

	[DataMember]
	public string FriendlyName { get; }
	[DataMember]
	public DeviceCategory Category { get; }
	// NB: This would ideally be readonly. It should be treated as such.
	[DataMember]
	public HashSet<Guid> FeatureIds { get; }
	[DataMember]
	public ImmutableArray<DeviceId> DeviceIds { get; }
	[DataMember]
	public int? MainDeviceIdIndex { get; }
	[DataMember]
	public string? SerialNumber { get; }

	public override bool Equals(object? obj) => Equals(obj as DeviceInformation);

	public bool Equals(DeviceInformation? other)
		=> other is not null &&
			FriendlyName == other.FriendlyName &&
			Category == other.Category &&
			FeatureIds.SetEquals(other.FeatureIds) &&
			DeviceIds.SequenceEqual(other.DeviceIds) &&
			MainDeviceIdIndex == other.MainDeviceIdIndex &&
			SerialNumber == other.SerialNumber;

	public override int GetHashCode() => HashCode.Combine(FriendlyName, Category, FeatureIds.Count, DeviceIds.Length(), MainDeviceIdIndex, SerialNumber);

	public static bool operator ==(DeviceInformation? left, DeviceInformation? right) => EqualityComparer<DeviceInformation>.Default.Equals(left, right);
	public static bool operator !=(DeviceInformation? left, DeviceInformation? right) => !(left == right);
}

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
