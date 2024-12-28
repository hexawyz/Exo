namespace DeviceTools.Usb;

public enum UsbEndpointUsageType : byte
{
	Data = 0b00,
	Feedback = 0b01,
	ExplicitFeedbackData = 0b10,
	Reserved = 0b11,
}
