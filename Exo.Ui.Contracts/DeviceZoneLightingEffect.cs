using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceZoneLightingEffect
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid ZoneId { get; init; }
	[DataMember(Order = 3)]
	public required LightingEffect? Effect { get; init; }
}
