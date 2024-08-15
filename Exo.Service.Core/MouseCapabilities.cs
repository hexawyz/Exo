namespace Exo.Service;

[Flags]
internal enum MouseCapabilities : byte
{
	None = 0x00,
	DynamicDpi = 0x01,
	SeparateXYDpi = 0x02,
	DpiPresets = 0x04,
	DpiPresetChange = 0x08,
	ConfigurableDpi = 0x10,
	ConfigurableDpiPresets = 0x20,
	ConfigurablePollingFrequency = 0x40,
	HardwareProfiles = 0x80,
}
