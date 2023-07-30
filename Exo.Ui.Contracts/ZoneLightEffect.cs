using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class ZoneLightEffect
{
	[DataMember(Order = 1)]
	public required Guid ZoneId { get; init; }
	[DataMember(Order = 2)]
	public required LightingEffect? Effect { get; init; }
}
