using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<EmbeddedMonitorInformation> EmbeddedMonitors { get; init; }
}
