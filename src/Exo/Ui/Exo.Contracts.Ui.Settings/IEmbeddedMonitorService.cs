using System.Runtime.Serialization;
using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "EmbeddedMonitors")]
public interface IEmbeddedMonitorService
{
	/// <summary>Watches information on all embedded monitor devices, including all the available monitors.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchEmbeddedMonitorDevices")]
	IAsyncEnumerable<EmbeddedMonitorDeviceInformation> WatchEmbeddedMonitorDevicesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SetBuiltInGraphics")]
	ValueTask SetBuiltInGraphicsAsync(EmbeddedMonitorSetBuiltInGraphicsRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetImage")]
	ValueTask SetImageAsync(EmbeddedMonitorSetImageRequest request, CancellationToken cancellationToken);
}

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
	public Rectangle CropZone { get; init; }
}
