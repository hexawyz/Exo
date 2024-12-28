using System.Runtime.InteropServices;

namespace DeviceTools.Usb;

[StructLayout(LayoutKind.Sequential)]
public struct UsbConfigurationDescriptor
{
	public UsbCommonDescriptor Common;
	public ushort TotalLength;
	public byte InterfaceCount;
	public byte ConfigurationValue;
	public byte ConfigurationStringIndex;
	public UsbConfigurationAttributes Attributes;
	public byte MaximumPower;
}
