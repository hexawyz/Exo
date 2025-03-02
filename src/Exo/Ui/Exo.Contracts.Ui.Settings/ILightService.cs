using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Lights")]
public interface ILightService
{
	[OperationContract(Name = "WatchLightDevices")]
	IAsyncEnumerable<LightDeviceInformation> WatchLightDevicesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchLightChanges")]
	IAsyncEnumerable<LightChangeNotification> WatchLightChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SwitchLight")]
	ValueTask SwitchLightAsync(LightSwitchRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetBrightness")]
	ValueTask SetBrightnessAsync(LightBrightnessRequest request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetTemperature")]
	ValueTask SetTemperatureAsync(LightTemperatureRequest request, CancellationToken cancellationToken);
}
