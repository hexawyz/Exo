using Exo.Service;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessCapabilitiesViewModel
{
	private readonly BrightnessCapabilities _capabilities;

	public LightingDeviceBrightnessCapabilitiesViewModel(BrightnessCapabilities capabilities) => _capabilities = capabilities;

	public byte MinimumLevel => _capabilities.MinimumValue;
	public byte MaximumLevel => _capabilities.MaximumValue;
}
