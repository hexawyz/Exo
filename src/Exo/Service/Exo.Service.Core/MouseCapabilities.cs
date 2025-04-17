namespace Exo.Service;

[Flags]
public enum MouseCapabilities : byte
{
	None = 0b00000000,
	DynamicDpi = 0b00000001,
	SeparateXYDpi = 0b00000010,
	DpiPresets = 0b00000100,
	DpiPresetChange = 0b00001000,
	ConfigurableDpi = 0b00010000,
	ConfigurableDpiPresets = 0b00100000,
	ConfigurablePollingFrequency = 0b01000000,
	HardwareProfiles = 0b10000000,
}
