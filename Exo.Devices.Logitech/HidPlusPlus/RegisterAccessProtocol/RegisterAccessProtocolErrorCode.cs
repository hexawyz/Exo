namespace Exo.Devices.Logitech.HidPlusPlus.RegisterAccessProtocol;

public enum RegisterAccessProtocolErrorCode : byte
{
	Success = 0x00,
	InvalidSubId = 0x01,
	InvalidAddress = 0x02,
	InvalidValue = 0x03,
	ConnectionFailed = 0x04,
	TooManyDevices = 0x05,
	AlreadyExists = 0x06,
	Busy = 0x07,
	UnknownDevice = 0x08,
	ResourceError = 0x09,
	InvalidState = 0x0A,
	InvalidParameter = 0x0B,
	WrongPinCode = 0x0C,
}
