using System.Runtime.Serialization;
using Exo.Contracts;

namespace Exo.Contracts.Ui.Settings;

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
