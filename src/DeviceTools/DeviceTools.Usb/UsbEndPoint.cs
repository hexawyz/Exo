using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbEndPoint
{
	public UsbEndPoint FromBytes(ReadOnlyMemory<byte> data)
	{
		if (data.Length != 6) throw new ArgumentException();
		return new(data);
	}

	private readonly ReadOnlyMemory<byte> _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbEndPoint() => throw new NotSupportedException();

	internal UsbEndPoint(ReadOnlyMemory<byte> data)
	{
		_data = data;
	}

	public ref readonly UsbEndpointDescriptor Descriptor => ref Unsafe.As<byte, UsbEndpointDescriptor>(ref Unsafe.AsRef(in _data.Span[0]));

	public byte Address => Descriptor.EndpointAddress;
	public UsbTransferType TransferType => Descriptor.TransferType;
	public UsbSynchronizationType SynchronizationType => Descriptor.SynchronizationType;
	public UsbEndpointUsageType UsageType => Descriptor.UsageType;
	public ushort MaximumPacketSize => Descriptor.MaximumPacketSize;
	public byte Interval => Descriptor.Interval;
}
