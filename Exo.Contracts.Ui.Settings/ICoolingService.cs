using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Cooling")]
public interface ICoolingService
{
	/// <summary>Watches information on all cooling devices, including the available coolers.</summary>
	/// <remarks>The availability status of devices is returned by <see cref="IDeviceService.WatchDevicesAsync(CancellationToken)"/>.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	[OperationContract(Name = "WatchCoolingDevices")]
	IAsyncEnumerable<CoolingDeviceInformation> WatchCoolingDevicesAsync(CancellationToken cancellationToken);
}
