using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorSetBuiltInGraphicsRequest
{
	[DataMember(Order = 1)]
	public Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public Guid MonitorId { get; init; }
	[DataMember(Order = 3)]
	public Guid GraphicsId { get; init; }
}
