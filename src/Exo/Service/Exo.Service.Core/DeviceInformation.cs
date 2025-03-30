using System.Collections.Immutable;
using System.Runtime.Serialization;
using DeviceTools;

namespace Exo.Service;

[DataContract]
[TypeId(0xF9477332, 0x3B69, 0x4CF9, 0xBA, 0x73, 0xFA, 0xA4, 0xA1, 0xD8, 0xBE, 0x21)]
public sealed class DeviceInformation : IEquatable<DeviceInformation?>
{
	public DeviceInformation(string friendlyName, DeviceCategory category, HashSet<Guid> supportedFeatureIds, ImmutableArray<DeviceId> deviceIds, int? mainDeviceIdIndex, string? serialNumber)
	{
		if (supportedFeatureIds is null) throw new ArgumentNullException(nameof(supportedFeatureIds));
		FriendlyName = friendlyName;
		Category = category;
		SupportedFeatureIds = supportedFeatureIds;
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
	public HashSet<Guid> SupportedFeatureIds { get; }
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
			SupportedFeatureIds.SetEquals(other.SupportedFeatureIds) &&
			DeviceIds.SequenceEqual(other.DeviceIds) &&
			MainDeviceIdIndex == other.MainDeviceIdIndex &&
			SerialNumber == other.SerialNumber;

	public override int GetHashCode() => HashCode.Combine(FriendlyName, Category, SupportedFeatureIds.Count, DeviceIds.Length(), MainDeviceIdIndex, SerialNumber);

	public static bool operator ==(DeviceInformation? left, DeviceInformation? right) => EqualityComparer<DeviceInformation>.Default.Equals(left, right);
	public static bool operator !=(DeviceInformation? left, DeviceInformation? right) => !(left == right);
}
