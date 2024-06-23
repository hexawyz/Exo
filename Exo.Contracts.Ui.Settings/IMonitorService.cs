using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Monitor")]
public interface IMonitorService
{
	[OperationContract(Name = "WatchSettings")]
	IAsyncEnumerable<MonitorSettingValue> WatchSettingsAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "GetMonitorInformation")]
	ValueTask<MonitorInformation> GetMonitorInformationAsync(DeviceRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetSettingValue")]
	ValueTask SetSettingValueAsync(MonitorSettingUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetBrightness")]
	ValueTask SetBrightnessAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetContrast")]
	ValueTask SetContrastAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetInputSource")]
	ValueTask SetInputSourceAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken);
}
