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

	// TODO: Make immutable somehow. (ImmutableSet will be unnecessarily slow)
	[DataMember(Order = 4)]
	public required HashSet<Guid> FeatureIds { get; init; }

	[DataMember(Order = 5)]
	public required ImmutableArray<DeviceId> DeviceIds { get; init; }

	/// <summary>Indicates if the device is connected.</summary>
	/// <remarks>
	/// <para>Changes to this status can be used to detect device availability changes.</para>
	/// </remarks>
	[DataMember(Order = 6)]
	public required bool IsAvailable { get; init; }
}
