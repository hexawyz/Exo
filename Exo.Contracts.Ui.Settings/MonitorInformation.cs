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
}
