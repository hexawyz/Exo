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
		{ DeviceCategory.Mouse, "\uE962" },
		{ DeviceCategory.Touchpad, "\uEFA5" },
		{ DeviceCategory.Gamepad, "\uE7FC" },
		{ DeviceCategory.Monitor, "\uE7F8" },
		{ DeviceCategory.Lighting, "\uE781" },
		{ DeviceCategory.UsbWireless, "\uECF1"},
		{ DeviceCategory.UsbWirelessReceiver, "\uECF1"},
		{ DeviceCategory.Speaker, "\uE7F5"},
		{ DeviceCategory.Microphone, "\uE720"},
		{ DeviceCategory.Headphone, "\uE7F6"},
		{ DeviceCategory.Headset, "\uE95B"},
		{ DeviceCategory.WirelessSpeaker, "\uF191"},
		{ DeviceCategory.AudioMixer, "\uF4C3" },
		{ DeviceCategory.Smartphone, "\uE8EA" },
	};

	// Priority numbers should be multiples of 2.
	// That way, if we find conflicting features of the same priority, we'll use the intermediate number to indicate an unknown device category.
	private static readonly Dictionary<string, (byte Priority, DeviceCategory Category)> DeviceFeatureToCategoryMapping = new()
	{
		{ KeyboardFeatureName, (4, DeviceCategory.Keyboard) },
		{ MouseFeatureName, (4, DeviceCategory.Mouse) },
		{ MonitorFeatureName, (6, DeviceCategory.Monitor) },
		{ LightingFeatureName, (2, DeviceCategory.Lighting) },
	};

	private readonly DeviceInformation _deviceInformation;

	public DeviceViewModel(DeviceInformation deviceInformation)
	{
		_deviceInformation = deviceInformation;
		var category = DeviceCategory.Other;
		int priority = 0;
		foreach (var feature in deviceInformation.FeatureTypeNames)
		{
			if (DeviceFeatureToCategoryMapping.TryGetValue(feature, out var details))
			{
				if (details.Priority == priority)
				{
					category = DeviceCategory.Other;
					priority++;
				}
				else if (details.Priority > priority)
				{
					category = details.Category;
					priority = details.Priority;
				}
			}
		}
		Category = category;
	}

	public string UniqueId => _deviceInformation.UniqueId;

	public string FriendlyName => _deviceInformation.FriendlyName;

	public DeviceCategory Category { get; }

	public string IconGlyph => DeviceCategoryToGlyphMapping[Category];
}
