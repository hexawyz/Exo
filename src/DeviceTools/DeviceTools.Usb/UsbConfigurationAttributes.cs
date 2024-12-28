namespace DeviceTools.Usb;

[Flags]
public enum UsbConfigurationAttributes : byte
{
	None = 0x00,
	RemoteWakeUp = 0x20,
	SelfPowered = 0x40,
	BusPowered = 0x80,
}
