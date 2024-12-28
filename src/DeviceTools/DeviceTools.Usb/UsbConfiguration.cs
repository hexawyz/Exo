using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeviceTools.Usb;

public readonly struct UsbConfiguration
{
	public UsbConfiguration FromBytes(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		if (data.Length < 9 || data.Length != Unsafe.As<byte, UsbConfigurationDescriptor>(ref data[0]).TotalLength) throw new ArgumentException();
		return new(data);
	}

	private readonly byte[] _data;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete]
	public UsbConfiguration() => throw new NotSupportedException();

	internal UsbConfiguration(byte[] data) => _data = data;

	public ref readonly UsbConfigurationDescriptor Descriptor => ref Unsafe.As<byte, UsbConfigurationDescriptor>(ref _data[0]);

	public UsbInterfaceCollection Interfaces => new UsbInterfaceCollection(_data);

	public byte Value => Descriptor.ConfigurationValue;
	public byte StringIndex => Descriptor.ConfigurationStringIndex;
	public UsbConfigurationAttributes Attributes => Descriptor.Attributes;
	public byte MaximumPower => Descriptor.MaximumPower;
}
