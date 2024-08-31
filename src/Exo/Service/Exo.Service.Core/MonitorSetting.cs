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

	VideoBlackLevelRed = 10,
	VideoBlackLevelGreen = 11,
	VideoBlackLevelBlue = 12,

	SixAxisSaturationControlRed = 13,
	SixAxisSaturationControlYellow = 14,
	SixAxisSaturationControlGreen = 15,
	SixAxisSaturationControlCyan = 16,
	SixAxisSaturationControlBlue = 17,
	SixAxisSaturationControlMagenta = 18,

	SixAxisHueControlRed = 19,
	SixAxisHueControlYellow = 20,
	SixAxisHueControlGreen = 21,
	SixAxisHueControlCyan = 22,
	SixAxisHueControlBlue = 23,
	SixAxisHueControlMagenta = 24,

	InputLag = 25,
	ResponseTime = 26,

	OsdLanguage = 27,
	PowerIndicator = 28,
}
