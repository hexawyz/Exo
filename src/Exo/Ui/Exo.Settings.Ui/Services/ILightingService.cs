using Exo.Service;

namespace Exo.Settings.Ui.Services;

internal interface ILightingService
{
	ValueTask SetLightingAsync(LightingConfiguration update, CancellationToken cancellationToken);
	ValueTask SetLightingAsync(LightingDeviceConfigurationUpdate update, CancellationToken cancellationToken);
	//ValueTask SetLightingAsync(MultiDeviceLightingUpdates update, CancellationToken cancellationToken);
}
