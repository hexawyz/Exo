namespace Exo.Devices.Monitors;

[Flags]
public enum SupportedFeatures : ulong
{
	None = 0x00000000,
	Capabilities = 0x00000001,
	Brightness = 0x00000002,
	Contrast = 0x00000004,
	AudioVolume = 0x00000008,
	InputSelect = 0x00000010,
	VideoGainRed = 0x00000020,
	VideoGainGreen = 0x00000040,
	VideoGainBlue = 0x00000080,
	SixAxisSaturationControlRed = 0x00000100,
	SixAxisSaturationControlYellow = 0x00000200,
	SixAxisSaturationControlGreen = 0x00000400,
	SixAxisSaturationControlCyan = 0x00000800,
	SixAxisSaturationControlBlue = 0x00001000,
	SixAxisSaturationControlMagenta = 0x00002000,
	SixAxisHueControlRed = 0x00004000,
	SixAxisHueControlYellow = 0x00008000,
	SixAxisHueControlGreen = 0x00010000,
	SixAxisHueControlCyan = 0x00020000,
	SixAxisHueControlBlue = 0x00040000,
	SixAxisHueControlMagenta = 0x00080000,
	OsdLanguage = 0x00100000,
	PowerIndicator = 0x00200000,
	InputLag = 0x00400000,
	ResponseTime = 0x00800000,
	BlueLightFilterLevel = 0x01000000,
}
