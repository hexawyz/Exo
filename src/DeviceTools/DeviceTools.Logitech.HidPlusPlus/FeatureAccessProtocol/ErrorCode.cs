namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

public enum ErrorCode : byte
{
	NoError = 0,
	Unknown = 1,
	InvalidArgument = 2,
	OutOfRange = 3,
	HardwareError = 4,
	LogitechInternal = 5,
	InvalidFeatureIndex = 6,
	InvalidFunctionId = 7,
	Busy = 8,
	Unsupported = 9,
}
