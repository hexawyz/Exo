namespace Exo.Devices.Logitech.HidPlusPlus.RegisterAccessProtocol;

// TODO: Fill with other known IDs.
public enum SubId : byte
{
	// TODO: Notifications

	// Register Access
	SetShortRegister = 0x80,
	GetShortRegister = 0x81,
	SetLongRegister = 0x82,
	GetLongRegister = 0x83,
	SetVeryLongRegister = 0x84,
	GetVeryLongRegister = 0x85,

	ErrorMessage = 0x8F,

	// TODO: HOT ?
}
