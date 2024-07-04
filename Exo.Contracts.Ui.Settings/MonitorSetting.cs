using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum MonitorSetting : uint
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	Brightness = 1,
	[EnumMember]
	Contrast = 2,
	[EnumMember]
	Sharpness = 3,
	[EnumMember]
	BlueLightFilterLevel = 4,
	[EnumMember]
	AudioVolume = 5,
	[EnumMember]
	InputSelect = 6,

	[EnumMember]
	VideoGainRed = 7,
	[EnumMember]
	VideoGainGreen = 8,
	[EnumMember]
	VideoGainBlue = 9,

	[EnumMember]
	VideoBlackLevelRed = 10,
	[EnumMember]
	VideoBlackLevelGreen = 11,
	[EnumMember]
	VideoBlackLevelBlue = 12,

	[EnumMember]
	SixAxisSaturationControlRed = 13,
	[EnumMember]
	SixAxisSaturationControlYellow = 14,
	[EnumMember]
	SixAxisSaturationControlGreen = 15,
	[EnumMember]
	SixAxisSaturationControlCyan = 16,
	[EnumMember]
	SixAxisSaturationControlBlue = 17,
	[EnumMember]
	SixAxisSaturationControlMagenta = 18,

	[EnumMember]
	SixAxisHueControlRed = 19,
	[EnumMember]
	SixAxisHueControlYellow = 20,
	[EnumMember]
	SixAxisHueControlGreen = 21,
	[EnumMember]
	SixAxisHueControlCyan = 22,
	[EnumMember]
	SixAxisHueControlBlue = 23,
	[EnumMember]
	SixAxisHueControlMagenta = 24,

	[EnumMember]
	InputLag = 25,
	[EnumMember]
	ResponseTime = 26,

	[EnumMember]
	OsdLanguage = 27,
	[EnumMember]
	PowerIndicator = 28,
}
