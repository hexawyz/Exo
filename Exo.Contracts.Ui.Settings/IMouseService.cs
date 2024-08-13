using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Mouse")]
public interface IMouseService
{
	[OperationContract(Name = "WatchMouseDevices")]
	IAsyncEnumerable<MouseDeviceInformation> WatchMouseDevicesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchDpiChanges")]
	IAsyncEnumerable<DpiChangeNotification> WatchDpiChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchDpiPresets")]
	IAsyncEnumerable<MouseDpiPresets> WatchDpiPresetsAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SetDpiPresets")]
	ValueTask SetDpiPresetsAsync(MouseDpiPresetUpdate request, CancellationToken cancellationToken);
}
