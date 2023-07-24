using System.ServiceModel;

namespace Exo.Ui.Contracts;

[ServiceContract]
public interface IDeviceService
{
	[OperationContract]
	IAsyncEnumerable<DeviceNotification> GetDevicesAsync(CancellationToken cancellationToken);
}
