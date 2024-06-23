using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MonitorInformation
{
	[DataMember(Order = 1)]
	public string? Capabilities { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<MonitorSetting> SupportedSettings { get; init; }
	[DataMember(Order = 3)]
	public ImmutableArray<NonContinuousValue> InputSelectSources { get; set; }
}
