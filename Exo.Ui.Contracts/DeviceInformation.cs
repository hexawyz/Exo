using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceInformation
{
	/// <summary>Gets the unique ID of this device in the system.</summary>
	/// <remarks>All device instances are given a unique ID in the form of <see cref="Guid"/> so that two identical devices can be discriminated easily.</remarks>
	[DataMember(Order = 1, IsRequired = true)]
	public required Guid Id { get; init; }

	[DataMember(Order = 2, IsRequired = true)]
	public required string FriendlyName { get; init; }

	[DataMember(Order = 3, IsRequired = true)]
	public required DeviceCategory Category { get; init; }

	[DataMember(Order = 4, IsRequired = true)]
	public required string DriverTypeName { get; init; }

	[DataMember(Order = 5)]
	public required ImmutableArray<string> FeatureTypeNames { get; init; }
}
