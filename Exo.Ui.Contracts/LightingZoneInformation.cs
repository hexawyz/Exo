using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class LightingZoneInformation
{
	[DataMember(Order = 1)]
	public required Guid ZoneId { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<string> SupportedEffectTypeNames { get; init; }
}
