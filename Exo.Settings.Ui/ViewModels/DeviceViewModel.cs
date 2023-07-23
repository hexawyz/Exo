using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DeviceViewModel : BindableObject
{
	private readonly DeviceInformation _deviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation) => _deviceInformation = deviceInformation;

	public string UniqueId => _deviceInformation.UniqueId;

	public string FriendlyName => _deviceInformation.FriendlyName;

	public string IconGlyph => "\uEBDE";
}
