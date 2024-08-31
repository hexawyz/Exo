using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Devices")]
public interface IDeviceService
{
	[OperationContract]
	IAsyncEnumerable<WatchNotification<DeviceInformation>> WatchDevicesAsync(CancellationToken cancellationToken);
}
