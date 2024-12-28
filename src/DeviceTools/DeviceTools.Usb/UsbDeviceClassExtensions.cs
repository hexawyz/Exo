namespace DeviceTools.Usb;

public static class UsbDeviceClassExtensions
{
	public static UsbDeviceClass ToUsbDeviceClass(this UsbDeviceDescriptorDeviceClass deviceClass)
		=> (UsbDeviceClass)(byte)deviceClass;

	public static UsbDeviceClass ToUsbDeviceClass(this UsbInterfaceDescriptorDeviceClass deviceClass)
		=> (UsbDeviceClass)(byte)deviceClass;
}
