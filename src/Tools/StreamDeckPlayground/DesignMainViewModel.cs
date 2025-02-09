using System.Collections.ObjectModel;
using System.Windows.Media;

namespace StreamDeckPlayground;

internal sealed class DesignMainViewModel
{
	public sealed class StreamDeckDevice
	{
		public string DeviceName { get; }
		public string SerialNumber { get; }
		public string FirmwareVersion { get; }
		public TimeSpan UsageTime { get; }
		public int ButtonColumnCount { get; }
		public int ButtonRowCount { get; }
		public int ButtonImageWidth { get; }
		public int ButtonImageHeight { get; }
		public int ScreensaverImageWidth { get; }
		public int ScreensaverImageHeight { get; }
		public ReadOnlyCollection<StreamDeckButton> Buttons { get; }
		public StreamDeckButton? SelectedButton { get; set; }

		public StreamDeckDevice(string deviceName, string serialNumber, string firmwareVersion, TimeSpan usageTime, int buttonColumnCount, int buttonRowCount, int buttonImageWidth, int buttonImageHeight, int screensaverImageWidth, int screensaverImageHeight)
		{
			DeviceName = deviceName;
			SerialNumber = serialNumber;
			FirmwareVersion = firmwareVersion;
			UsageTime = usageTime;
			ButtonColumnCount = buttonColumnCount;
			ButtonRowCount = buttonRowCount;
			ButtonImageWidth = buttonImageWidth;
			ButtonImageHeight = buttonImageHeight;
			ScreensaverImageWidth = screensaverImageWidth;
			ScreensaverImageHeight = screensaverImageHeight;
			Buttons = Array.AsReadOnly(Enumerable.Range(0, buttonColumnCount * buttonRowCount).Select(i => new StreamDeckButton()).ToArray());
			SelectedButton = Buttons[2];
		}
	}

	public sealed class StreamDeckButton
	{
		public int Width => 96;
		public int Height => 96;

		public byte Red { get; set; } = 55;
		public byte Green { get; set; } = 207;
		public byte Blue { get; set; } = 29;
		public Color Color => Color.FromArgb(255, Red, Green, Blue);
		public string HtmlColorCode => $"#{Red:X2}{Green:X2}{Blue:X2}";
	}

	public ReadOnlyObservableCollection<StreamDeckDevice> Devices { get; }
	public StreamDeckDevice? SelectedDevice { get; set; }

	public DesignMainViewModel()
	{
		var device = new StreamDeckDevice(@"\\HID\Whatever", "SN0123456789", "42.42.42", TimeSpan.FromDays(21, 8), 8, 4, 96, 96, 1024, 600);
		Devices = new([device]);
		SelectedDevice = device;
	}
}
