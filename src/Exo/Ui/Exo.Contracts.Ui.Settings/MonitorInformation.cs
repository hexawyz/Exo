using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MonitorInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public string? Capabilities { get; init; }
	[DataMember(Order = 3)]
	public required ImmutableArray<MonitorSetting> SupportedSettings { get; init; }
	[DataMember(Order = 4)]
	public required ImmutableArray<NonContinuousValue> InputSelectSources { get; set; }
	[DataMember(Order = 5)]
	public required ImmutableArray<NonContinuousValue> InputLagLevels { get; set; }
	[DataMember(Order = 6)]
	public required ImmutableArray<NonContinuousValue> ResponseTimeLevels { get; set; }
	[DataMember(Order = 7)]
	public required ImmutableArray<NonContinuousValue> OsdLanguages { get; set; }
}
