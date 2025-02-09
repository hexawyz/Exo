using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorConfigurationUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid MonitorId { get; init; }
	[DataMember(Order = 3)]
	public required Guid GraphicsId { get; init; }
	[DataMember(Order = 5)]
	public EmbeddedMonitorImageConfiguration? ImageConfiguration { get; init; }
}
