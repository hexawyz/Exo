using System.Runtime.InteropServices;

namespace DeviceTools.Usb;

[StructLayout(LayoutKind.Sequential)]
public struct UsbDeviceDescriptor
{
	public UsbCommonDescriptor Common;
	public ushort UsbVersion;
	public UsbDeviceDescriptorDeviceClass DeviceClass;
	public byte DeviceSubClass;
	public byte DeviceProtocol;
	public byte MaxPacketSize0;
	public ushort VendorId;
	public ushort ProductId;
	public ushort DeviceVersion;
	public byte ManufacturerStringIndex;
	public byte ProductStringIndex;
	public byte SerialNumberStringIndex;
	public byte ConfigurationCount;
}
