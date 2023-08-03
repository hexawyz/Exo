using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class LightingZoneInformation
{
	[DataMember(Order = 1)]
	public required Guid ZoneId { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<Guid> SupportedEffectIds { get; init; }
}
