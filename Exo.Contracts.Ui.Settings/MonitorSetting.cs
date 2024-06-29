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
	AudioVolume = 3,
	[EnumMember]
	InputSelect = 4,

	[EnumMember]
	VideoGainRed = 5,
	[EnumMember]
	VideoGainGreen = 6,
	[EnumMember]
	VideoGainBlue = 7,

	[EnumMember]
	SixAxisSaturationControlRed = 8,
	[EnumMember]
	SixAxisSaturationControlYellow = 9,
	[EnumMember]
	SixAxisSaturationControlGreen = 10,
	[EnumMember]
	SixAxisSaturationControlCyan = 11,
	[EnumMember]
	SixAxisSaturationControlBlue = 12,
	[EnumMember]
	SixAxisSaturationControlMagenta = 13,

	[EnumMember]
	SixAxisHueControlRed = 14,
	[EnumMember]
	SixAxisHueControlYellow = 15,
	[EnumMember]
	SixAxisHueControlGreen = 16,
	[EnumMember]
	SixAxisHueControlCyan = 17,
	[EnumMember]
	SixAxisHueControlBlue = 18,
	[EnumMember]
	SixAxisHueControlMagenta = 19,
}
