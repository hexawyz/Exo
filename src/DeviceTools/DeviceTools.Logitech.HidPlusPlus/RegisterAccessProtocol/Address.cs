namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

public enum Address : byte
{
	EnableHidPlusPlusNotifications = 0x00,
	ConnectionState = 0x02,
	NonVolatileAndPairingInformation = 0xB5,
	BoltSerialNumber = 0xFB,
}
