using System.ServiceModel;

namespace Exo.Ui.Contracts;

[ServiceContract(Name = "Monitor")]
public interface IMonitorService
{
	[OperationContract(Name = "WatchSettings")]
	IAsyncEnumerable<MonitorSettingValue> WatchSettingsAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "GetSupportedSettings")]
	ValueTask<MonitorSupportedSettings> GetSupportedSettingsAsync(DeviceRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetSettingValue")]
	ValueTask SetSettingValueAsync(MonitorSettingUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetBrightness")]
	ValueTask SetBrightnessAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetContrast")]
	ValueTask SetContrastAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken);
}
