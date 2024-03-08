using System;
using Exo.Ui.Contracts;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

public class DeviceCategoryToGlyphConverter : IValueConverter
{
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
		{ DeviceCategory.GraphicsAdapter, "\uF211" },
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
		{ DeviceCategory.Battery, "\uF5FC" },
		{ DeviceCategory.MouseDock, "\uE95F" },
		{ DeviceCategory.MousePad, "\uE7FB" },
	};

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not DeviceCategory category)
		{
			category = DeviceCategory.Other;
		}

		return DeviceCategoryToGlyphMapping[category];
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
