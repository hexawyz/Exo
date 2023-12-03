using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum DeviceIdSource : byte
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	PlugAndPlay = 1,
	[EnumMember]
	Display = 2,
	[EnumMember]
	Pci = 3,
	[EnumMember]
	Usb = 4,
	[EnumMember]
	Bluetooth = 5,
	[EnumMember]
	BluetoothLowEnergy = 6,
	[EnumMember]
	Hid = 64,
	[EnumMember]
	EQuad = 128,
}
