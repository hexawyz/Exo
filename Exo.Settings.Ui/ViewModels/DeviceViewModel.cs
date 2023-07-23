using System.Collections.Generic;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class DeviceViewModel : BindableObject
{
	public const string LightingFeatureName = "Exo.Features.ILightingDeviceFeature";
	public const string MonitorFeatureName = "Exo.Features.IMonitorDeviceFeature";
	public const string KeyboardFeatureName = "Exo.Features.IKeyboardDeviceFeature";
	public const string MouseFeatureName = "Exo.Features.IMouseDeviceFeature";

	private static readonly Dictionary<DeviceCategory, string> DeviceCategoryToGlyphMapping = new()
	{
		{ DeviceCategory.Other, "\uEBDE" },
		{ DeviceCategory.Usb, "\uE88E" },
		{ DeviceCategory.Keyboard, "\uE92E" },
		{ DeviceCategory.Numpad, "\uF261" },
		{ DeviceCategory.Mouse, "\uE962" },
		{ DeviceCategory.Touchpad, "\uEFA5" },
		{ DeviceCategory.Gamepad, "\uE7FC" },
		{ DeviceCategory.Monitor, "\uE7F8" },
		{ DeviceCategory.Lighting, "\uE781" },
		{ DeviceCategory.UsbWirelessNetwork, "\uECF1"},
		{ DeviceCategory.UsbWirelessReceiver, "\uECF1"},
		{ DeviceCategory.Speaker, "\uE7F5"},
		{ DeviceCategory.Headphone, "\uE7F6"},
		{ DeviceCategory.Microphone, "\uE720"},
		{ DeviceCategory.Headset, "\uE95B"},
		{ DeviceCategory.WirelessSpeaker, "\uF191"},
		{ DeviceCategory.AudioMixer, "\uF4C3" },
		{ DeviceCategory.Webcam, "\uE960" },
		{ DeviceCategory.Camera, "\uE722" },
		{ DeviceCategory.Smartphone, "\uE8EA" },
	};

	private readonly DeviceInformation _deviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation)
	{
		_deviceInformation = deviceInformation;
	}

	public string UniqueId => _deviceInformation.UniqueId;

	public string FriendlyName => _deviceInformation.FriendlyName;

	public DeviceCategory Category => _deviceInformation.Category;

	public string IconGlyph => DeviceCategoryToGlyphMapping[Category];
}
