using System.Collections.Immutable;
using Exo.Monitors;

namespace Exo.Service;

public readonly struct MonitorInformation(
	Guid deviceId,
	ImmutableArray<MonitorSetting> supportedSettings,
	ImmutableArray<NonContinuousValueDescription> inputSelectSources,
	ImmutableArray<NonContinuousValueDescription> inputLagLevels,
	ImmutableArray<NonContinuousValueDescription> responseTimeLevels,
	ImmutableArray<NonContinuousValueDescription> osdLanguages)
{
	public Guid DeviceId { get; } = deviceId;
	public ImmutableArray<MonitorSetting> SupportedSettings { get; } = supportedSettings;
	public ImmutableArray<NonContinuousValueDescription> InputSelectSources { get; } = inputSelectSources;
	public ImmutableArray<NonContinuousValueDescription> InputLagLevels { get; } = inputLagLevels;
	public ImmutableArray<NonContinuousValueDescription> ResponseTimeLevels { get; } = responseTimeLevels;
	public ImmutableArray<NonContinuousValueDescription> OsdLanguages { get; } = osdLanguages;
}
