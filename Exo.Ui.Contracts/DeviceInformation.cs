using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceInformation
{
	[DataMember(Order = 1, IsRequired = true)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2, IsRequired = true)]
	public required string FriendlyName { get; init; }
	[DataMember(Order = 3, IsRequired = true)]
	public required DeviceCategory Category { get; init; }
	[DataMember(Order = 4, IsRequired = true)]
	public required string DriverTypeName { get; init; }
	[DataMember(Order = 5)]
	public required ImmutableArray<string> FeatureTypeNames { get; init; }
}
