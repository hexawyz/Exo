using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessCapabilitiesViewModel
{
	private readonly LightingBrightnessCapabilities _capabilities;

	public LightingDeviceBrightnessCapabilitiesViewModel(LightingBrightnessCapabilities capabilities) => _capabilities = capabilities;

	public byte MinimumLevel => _capabilities.MinimumBrightness;
	public byte MaximumLevel => _capabilities.MaximumBrightness;
}
