using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class DeviceLightingUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required bool ShouldPersist { get; init; }
	[DataMember(Order = 3)]
	public byte BrightnessLevel { get; init; }
	[DataMember(Order = 4)]
	public required ImmutableArray<ZoneLightEffect> ZoneEffects { get; init; }
}
