using System;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BindableObject
{
	public const string LightingFeatureName = "Exo.Features.ILightingDeviceFeature";
	public const string MonitorFeatureName = "Exo.Features.IMonitorDeviceFeature";
	public const string KeyboardFeatureName = "Exo.Features.IKeyboardDeviceFeature";
	public const string MouseFeatureName = "Exo.Features.IMouseDeviceFeature";

	private readonly DeviceInformation _deviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation)
	{
		_deviceInformation = deviceInformation;
	}

	public Guid UniqueId => _deviceInformation.DeviceId;

	public string FriendlyName => _deviceInformation.FriendlyName;

	public DeviceCategory Category => _deviceInformation.Category;
}
