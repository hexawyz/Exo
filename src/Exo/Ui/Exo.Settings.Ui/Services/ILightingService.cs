using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.Services;

internal interface ILightingService
{
	ValueTask SetLightingAsync(DeviceLightingUpdate update, CancellationToken cancellationToken);
	//ValueTask SetLightingAsync(MultiDeviceLightingUpdates update, CancellationToken cancellationToken);
}
