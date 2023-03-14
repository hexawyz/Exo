namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[Flags]
public enum DeviceConnectionFlags : byte
{
	SoftwarePresent = 0x10,
	LinkEncrypted = 0x20,
	LinkEstablished = 0x40,
	PacketWithPayload = 0x80,
}
