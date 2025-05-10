using Exo.Service;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class LightingDeviceBrightnessCapabilitiesViewModel
{
	private readonly BrightnessCapabilities _capabilities;

	public LightingDeviceBrightnessCapabilitiesViewModel(BrightnessCapabilities capabilities) => _capabilities = capabilities;

	public byte MinimumLevel => _capabilities.MinimumValue;
	public byte MaximumLevel => _capabilities.MaximumValue;
}
