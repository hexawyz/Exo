using System.Collections.Immutable;
using System.Runtime.Serialization;
using DeviceTools;

namespace Exo.Service;

[DataContract]
public sealed class DeviceInformation : IEquatable<DeviceInformation?>
{
	public DeviceInformation(string friendlyName, DeviceCategory category, HashSet<Guid> featureIds, ImmutableArray<DeviceId> deviceIds)
	{
		if (featureIds is null) throw new ArgumentNullException(nameof(featureIds));
		if (deviceIds.IsDefault) throw new ArgumentNullException(nameof(deviceIds));
		FriendlyName = friendlyName;
		Category = category;
		FeatureIds = featureIds;
		DeviceIds = deviceIds;
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

	public override bool Equals(object? obj) => Equals(obj as DeviceInformation);

	public bool Equals(DeviceInformation? other)
		=> other is not null &&
			FriendlyName == other.FriendlyName &&
			Category == other.Category &&
			FeatureIds.SetEquals(other.FeatureIds) &&
			DeviceIds.SequenceEqual(other.DeviceIds);

	public override int GetHashCode() => HashCode.Combine(FriendlyName, Category, FeatureIds.Count, DeviceIds.Length());

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
	public bool IsAvailable { get; }

	public DeviceStateInformation(Guid id, string friendlyName, string userFriendlyName, DeviceCategory category, HashSet<Guid> featureIds, ImmutableArray<DeviceId> deviceIds, bool isAvailable)
	{
		Id = id;
		FriendlyName = friendlyName;
		UserFriendlyName = userFriendlyName;
		Category = category;
		FeatureIds = featureIds;
		DeviceIds = deviceIds;
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
		IsAvailable == other.IsAvailable;

	public override int GetHashCode() => HashCode.Combine(Id, FriendlyName, UserFriendlyName, Category, FeatureIds, DeviceIds, IsAvailable);

	public static bool operator ==(DeviceStateInformation? left, DeviceStateInformation? right) => EqualityComparer<DeviceStateInformation>.Default.Equals(left, right);
	public static bool operator !=(DeviceStateInformation? left, DeviceStateInformation? right) => !(left == right);
}
