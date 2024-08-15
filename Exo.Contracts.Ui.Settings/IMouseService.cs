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

	[OperationContract(Name = "SetActiveDpiPreset")]
	ValueTask SetActiveDpiPresetAsync(MouseActiveDpiPresetUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetDpiPresets")]
	ValueTask SetDpiPresetsAsync(MouseDpiPresetUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "WatchPollingFrequencies")]
	IAsyncEnumerable<MousePollingFrequencyUpdate> WatchPollingFrequenciesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SetPollingFrequency")]
	ValueTask SetPollingFrequencyAsync(MousePollingFrequencyUpdate request, CancellationToken cancellationToken);
}
