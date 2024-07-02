namespace Exo.Service;

public enum MonitorSetting : uint
{
	Unknown = 0,

	Brightness = 1,
	Contrast = 2,
	Sharpness = 3,
	BlueLightFilterLevel = 4,

	AudioVolume = 5,
	InputSelect = 6,

	VideoGainRed = 7,
	VideoGainGreen = 8,
	VideoGainBlue = 9,

	SixAxisSaturationControlRed = 10,
	SixAxisSaturationControlYellow = 11,
	SixAxisSaturationControlGreen = 12,
	SixAxisSaturationControlCyan = 13,
	SixAxisSaturationControlBlue = 14,
	SixAxisSaturationControlMagenta = 15,

	SixAxisHueControlRed = 16,
	SixAxisHueControlYellow = 17,
	SixAxisHueControlGreen = 18,
	SixAxisHueControlCyan = 19,
	SixAxisHueControlBlue = 20,
	SixAxisHueControlMagenta = 21,

	InputLag = 22,
	ResponseTime = 23,

	OsdLanguage = 24,
	PowerIndicator = 25,
}
