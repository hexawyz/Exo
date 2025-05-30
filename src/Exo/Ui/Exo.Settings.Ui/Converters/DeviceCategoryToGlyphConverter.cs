using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

public sealed partial class DeviceCategoryToGlyphConverter : IValueConverter
{
	private static readonly Dictionary<DeviceCategory, string> DeviceCategoryToGlyphMapping = new()
	{
		{ DeviceCategory.Other, "\uEBDE" },
		{ DeviceCategory.Motherboard, "\uF0B9" },
		{ DeviceCategory.MemoryModule, "\uE950" },
		{ DeviceCategory.Processor, "\uE950" },
		{ DeviceCategory.Cooler, "\uE9CA" },
		{ DeviceCategory.Usb, "\uE88E" },
		{ DeviceCategory.Keyboard, "\uE92E" },
		{ DeviceCategory.Numpad, "\uF261" },
		{ DeviceCategory.Mouse, "\uE962" },
		{ DeviceCategory.Touchpad, "\uEFA5" },
		{ DeviceCategory.Gamepad, "\uE7FC" },
		{ DeviceCategory.Monitor, "\uE7F8" },
		{ DeviceCategory.GraphicsAdapter, "\uF211" },
		{ DeviceCategory.Light, "\uEA80" },
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
		{ DeviceCategory.PowerSupply, "\uE945" },
		{ DeviceCategory.MouseDock, "\uE945" },
		{ DeviceCategory.MousePad, "\uE7FB" },
	};

	public static string GetGlyph(DeviceCategory category)
		=> DeviceCategoryToGlyphMapping[category];

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not DeviceCategory category)
			category = DeviceCategory.Other;

		return GetGlyph(category);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
