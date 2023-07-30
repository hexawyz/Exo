using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceLightingEffects
{
	[DataMember(Order = 1)]
	public required Guid UniqueId { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<ZoneLightEffect> ZoneEffects { get; init; }
}
