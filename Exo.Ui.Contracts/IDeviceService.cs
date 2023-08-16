using System.ServiceModel;

namespace Exo.Ui.Contracts;

[ServiceContract(Name = "Devices")]
public interface IDeviceService
{
	[OperationContract]
	IAsyncEnumerable<WatchNotification<DeviceInformation>> WatchDevicesAsync(CancellationToken cancellationToken);

	[OperationContract]
	ValueTask<ExtendedDeviceInformation> GetExtendedDeviceInformationAsync(DeviceRequest request, CancellationToken cancellationToken);

	[OperationContract]
	IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync(CancellationToken cancellationToken);
}
