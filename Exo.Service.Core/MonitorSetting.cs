namespace Exo.Service;

public enum MonitorSetting : uint
{
	Unknown = 0,
	Brightness = 1,
	Contrast = 2,
	AudioVolume = 3,
	InputSelect = 4,

	VideoGainRed = 5,
	VideoGainGreen = 6,
	VideoGainBlue = 7,

	SixAxisSaturationControlRed = 8,
	SixAxisSaturationControlYellow = 9,
	SixAxisSaturationControlGreen = 10,
	SixAxisSaturationControlCyan = 11,
	SixAxisSaturationControlBlue = 12,
	SixAxisSaturationControlMagenta = 13,

	SixAxisHueControlRed = 14,
	SixAxisHueControlYellow = 15,
	SixAxisHueControlGreen = 16,
	SixAxisHueControlCyan = 17,
	SixAxisHueControlBlue = 18,
	SixAxisHueControlMagenta = 19,
}
