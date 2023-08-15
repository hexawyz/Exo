using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum VendorIdSource : byte
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	Pci = 1,
	[EnumMember]
	Usb = 2,
	[EnumMember]
	Bluetooth = 3,
}
