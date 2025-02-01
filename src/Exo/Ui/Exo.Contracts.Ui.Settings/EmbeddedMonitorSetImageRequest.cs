using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorSetImageRequest
{
	[DataMember(Order = 1)]
	public Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public Guid MonitorId { get; init; }
	[DataMember(Order = 3)]
	public UInt128 ImageId { get; init; }
	[DataMember(Order = 4)]
	public Rectangle CropRegion { get; init; }
}
