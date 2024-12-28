using System.Runtime.InteropServices;

namespace DeviceTools.Usb;

[StructLayout(LayoutKind.Sequential)]
public struct UsbCommonDescriptor
{
	public byte Length;
	public UsbDescriptorType DescriptorType;
}
