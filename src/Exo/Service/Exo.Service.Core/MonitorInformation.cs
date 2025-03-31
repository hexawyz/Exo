using System.Collections.Immutable;
using Exo.Monitors;

namespace Exo.Service;

public readonly struct MonitorInformation
{
	public required Guid DeviceId { get; init; }
	public required ImmutableArray<MonitorSetting> SupportedSettings { get; init; }
	public required ImmutableArray<NonContinuousValueDescription> InputSelectSources { get; init; }
	public required ImmutableArray<NonContinuousValueDescription> InputLagLevels { get; init; }
	public required ImmutableArray<NonContinuousValueDescription> ResponseTimeLevels { get; init; }
	public required ImmutableArray<NonContinuousValueDescription> OsdLanguages { get; init; }
}
