using System.Runtime.InteropServices;

namespace DeviceTools.Usb;

[StructLayout(LayoutKind.Sequential)]
public struct UsbInterfaceDescriptor
{
	public UsbCommonDescriptor Common;
	public byte InterfaceIndex;
	public byte AlternateSettingIndex;
	public byte EndpointCount;
	public byte InterfaceClass;
	public byte InterfaceSubClass;
	public byte InterfaceProtocol;
	public byte InterfaceStringIndex;
}
