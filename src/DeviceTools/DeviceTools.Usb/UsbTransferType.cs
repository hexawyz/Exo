namespace DeviceTools.Usb;

public enum UsbTransferType : byte
{
	Control = 0b00,
	Isochronous = 0b01,
	Bulk = 0b10,
	Interrupt = 0b11,
}
