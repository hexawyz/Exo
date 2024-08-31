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

	private readonly ImmutableArray<ZoneLightEffect> _zoneEffects;
	[DataMember(Order = 4)]
	public required ImmutableArray<ZoneLightEffect> ZoneEffects
	{
		get => _zoneEffects.NotNull();
		init => _zoneEffects = value.NotNull();
	}
}
