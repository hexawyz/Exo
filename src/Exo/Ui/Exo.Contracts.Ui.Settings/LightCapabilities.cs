using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[Flags]
[DataContract]
public enum LightCapabilities : byte
{
	[EnumMember]
	None = 0b00000000,
	[EnumMember]
	Brightness = 0b00000001,
	[EnumMember]
	Temperature = 0b00000010,
	[EnumMember]
	Color = 0b00000100,
}
