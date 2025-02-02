using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum ImageFormat : uint
{
	[EnumMember]
	Raw = 0,
	[EnumMember]
	Bitmap = 1,
	[EnumMember]
	Gif = 2,
	[EnumMember]
	Jpeg = 3,
	[EnumMember]
	Png = 4,
	[EnumMember]
	WebPLossy = 5,
	[EnumMember]
	WebPLossless = 6,
}

[DataContract]
[Flags]
public enum ImageFormats : uint
{
	[EnumMember]
	Raw = 0b00000001,
	[EnumMember]
	Bitmap = 0b00000010,
	[EnumMember]
	Gif = 0b00000100,
	[EnumMember]
	Jpeg = 0b00001000,
	[EnumMember]
	Png = 0b00010000,
	[EnumMember]
	WebPLossy = 0b00100000,
	[EnumMember]
	WebPLossless = 0b01000000,
}
