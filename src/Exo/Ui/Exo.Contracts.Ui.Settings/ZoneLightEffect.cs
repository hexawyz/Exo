using System.Runtime.Serialization;
using Exo.Contracts;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class ZoneLightEffect
{
	[DataMember(Order = 1)]
	public required Guid ZoneId { get; init; }
	[DataMember(Order = 2)]
	public required LightingEffect? Effect { get; init; }
}
