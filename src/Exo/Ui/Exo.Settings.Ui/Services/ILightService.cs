namespace Exo.Settings.Ui.Services;

internal interface ILightService
{
	Task SwitchLightAsync(Guid deviceId, Guid lightId, bool isOn, CancellationToken cancellationToken);
	Task SetBrightnessAsync(Guid deviceId, Guid lightId, byte brightness, CancellationToken cancellationToken);
	Task SetTemperatureAsync(Guid deviceId, Guid lightId, uint temperature, CancellationToken cancellationToken);
}
