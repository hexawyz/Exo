using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum VendorIdSource : byte
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	PlugAndPlay = 1,
	[EnumMember]
	Pci = 2,
	[EnumMember]
	Usb = 3,
	[EnumMember]
	Bluetooth = 4,
}
