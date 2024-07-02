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
	SixAxisSaturationControlRed = 10,
	[EnumMember]
	SixAxisSaturationControlYellow = 11,
	[EnumMember]
	SixAxisSaturationControlGreen = 12,
	[EnumMember]
	SixAxisSaturationControlCyan = 13,
	[EnumMember]
	SixAxisSaturationControlBlue = 14,
	[EnumMember]
	SixAxisSaturationControlMagenta = 15,

	[EnumMember]
	SixAxisHueControlRed = 16,
	[EnumMember]
	SixAxisHueControlYellow = 17,
	[EnumMember]
	SixAxisHueControlGreen = 18,
	[EnumMember]
	SixAxisHueControlCyan = 19,
	[EnumMember]
	SixAxisHueControlBlue = 20,
	[EnumMember]
	SixAxisHueControlMagenta = 21,

	[EnumMember]
	InputLag = 22,
	[EnumMember]
	ResponseTime = 23,

	[EnumMember]
	OsdLanguage = 24,
	[EnumMember]
	PowerIndicator = 25,
}
