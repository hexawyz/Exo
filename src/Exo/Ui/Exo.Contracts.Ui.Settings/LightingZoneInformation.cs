using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightingZoneInformation
{
	[DataMember(Order = 1)]
	public required Guid ZoneId { get; init; }

	private readonly ImmutableArray<Guid> _supportedEffectIds;

	[DataMember(Order = 2)]
	public required ImmutableArray<Guid> SupportedEffectIds
	{
		get => _supportedEffectIds;
		init => _supportedEffectIds = value.NotNull();
	}
}
