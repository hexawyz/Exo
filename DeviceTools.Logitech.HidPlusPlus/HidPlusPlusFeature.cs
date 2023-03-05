namespace DeviceTools.Logitech.HidPlusPlus;

public enum HidPlusPlusFeature : ushort
{
	Root = 0x0000,
	FeatureSet = 0x0001,
	FeatureInformation = 0x002,
	DeviceInformation = 0x0003,
	UnitId = 0x0004,
	DeviceNameAndType = 0x0005,
	DeviceFriendlyName = 0x0007,
	ConfigurationChange = 0x0020,
	BatteryUnifiedLevelStatus = 0x1000,
	ChangeHost = 0x1814,
	BacklightV1 = 0x1981,
	BacklightV2 = 0x1982,
	KeyboardReprogrammableKeysV1 = 0x1B00,
	KeyboardReprogrammableKeysV2 = 0x1B01,
	KeyboardReprogrammableKeysV3 = 0x1B02,
	KeyboardReprogrammableKeysV4 = 0x1B03,
	KeyboardReprogrammableKeysV5 = 0x1B04,
	WirelessDeviceStatus = 0x1D4B,
	FnInversionMultiHost = 0x40A3,
	LockKeyState = 0x4220,
	DisableKeys = 0x4521,
}
