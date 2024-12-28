using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbInterface
{
	public UsbInterface FromBytes(ReadOnlyMemory<byte> data)
	{
		if (data.Length < 9 || data.Length != 9 + 6 * Unsafe.As<byte, UsbInterfaceDescriptor>(ref Unsafe.AsRef(in data.Span[0])).EndpointCount) throw new ArgumentException();
		return new(data);
	}

	private readonly ReadOnlyMemory<byte> _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbInterface() => throw new NotSupportedException();

	internal UsbInterface(ReadOnlyMemory<byte> data) => _data = data;

	public ref readonly UsbInterfaceDescriptor Descriptor => ref Unsafe.As<byte, UsbInterfaceDescriptor>(ref Unsafe.AsRef(in _data.Span[0]));

	public UsbEndpointCollection Endpoints => new UsbEndpointCollection(_data);

	public byte Index => Descriptor.InterfaceIndex;
	public byte AlternateSettingIndex => Descriptor.AlternateSettingIndex;
	public byte Class => Descriptor.InterfaceClass;
	public byte SubClass => Descriptor.InterfaceSubClass;
	public byte Protocol => Descriptor.InterfaceProtocol;
	public byte StringIndex => Descriptor.InterfaceStringIndex;
}
