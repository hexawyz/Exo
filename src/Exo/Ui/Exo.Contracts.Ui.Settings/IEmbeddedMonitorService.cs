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

	[OperationContract(Name = "WatchConfigurationUpdates")]
	IAsyncEnumerable<EmbeddedMonitorConfigurationUpdate> WatchConfigurationUpdatesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SetBuiltInGraphics")]
	ValueTask SetBuiltInGraphicsAsync(EmbeddedMonitorSetBuiltInGraphicsRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetImage")]
	ValueTask SetImageAsync(EmbeddedMonitorSetImageRequest request, CancellationToken cancellationToken);
}
