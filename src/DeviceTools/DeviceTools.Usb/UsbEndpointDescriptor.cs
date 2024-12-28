using System.Runtime.InteropServices;

namespace DeviceTools.Usb;

[StructLayout(LayoutKind.Sequential)]
public struct UsbEndpointDescriptor
{
	public UsbCommonDescriptor Common;
	public byte EndpointAddress;
	private byte _attributes;

	public UsbTransferType TransferType
	{
		get => (UsbTransferType)(_attributes & 0b00000011);
		set => _attributes = (byte)((_attributes & 0b11111100) | (byte)value & 0b00000011);
	}

	public UsbSynchronizationType SynchronizationType
	{
		get => (UsbSynchronizationType)(_attributes >>> 2 & 0b00000011);
		set => _attributes = (byte)((_attributes & 0b11110011) | ((byte)value & 0b00000011) << 2);
	}

	public UsbEndpointUsageType UsageType
	{
		get => (UsbEndpointUsageType)(_attributes >>> 4 & 0b00000011);
		set => _attributes = (byte)((_attributes & 0b11001111) | ((byte)value & 0b00000011) << 4);
	}

	public byte MaximumPacketSize;
	public byte Interval;
}
