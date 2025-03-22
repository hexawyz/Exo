using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Sensors")]
public interface ISensorService
{
	/// <summary>Watches information on all sensor devices, including the available sensors.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchSensorDevices")]
	IAsyncEnumerable<SensorDeviceInformation> WatchSensorDevicesAsync(CancellationToken cancellationToken);
}
