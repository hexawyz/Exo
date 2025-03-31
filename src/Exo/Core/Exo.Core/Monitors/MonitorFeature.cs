namespace Exo.Monitors;

public enum MonitorFeature : byte
{
	Other = 0,

	Brightness,
	Contrast,
	Sharpness,
	AudioVolume,

	InputSelect,

	VideoGainRed,
	VideoGainGreen,
	VideoGainBlue,

	VideoBlackLevelRed,
	VideoBlackLevelGreen,
	VideoBlackLevelBlue,

	SixAxisSaturationControlRed,
	SixAxisSaturationControlYellow,
	SixAxisSaturationControlGreen,
	SixAxisSaturationControlCyan,
	SixAxisSaturationControlBlue,
	SixAxisSaturationControlMagenta,

	SixAxisHueControlRed,
	SixAxisHueControlYellow,
	SixAxisHueControlGreen,
	SixAxisHueControlCyan,
	SixAxisHueControlBlue,
	SixAxisHueControlMagenta,

	InputLag,
	ResponseTime,
	BlueLightFilterLevel,

	OsdLanguage,
	PowerIndicator,
}
