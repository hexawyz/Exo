using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MonitorSupportedSettings
{
	[DataMember(Order = 1)]
	public required ImmutableArray<MonitorSetting> Settings { get; init; }
}
