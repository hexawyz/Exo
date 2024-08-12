using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[Flags]
[DataContract]
public enum MouseDpiCapabilities : byte
{
	[EnumMember]
	None = 0x00,
	[EnumMember]
	DynamicDpi = 0x01,
	[EnumMember]
	SeparateXYDpi = 0x02,
	[EnumMember]
	DpiPresets = 0x04,
	[EnumMember]
	DpiPresetsChange = 0x08,
	[EnumMember]
	ConfigurableDpi = 0x10,
	[EnumMember]
	ConfigurableDpiPresets = 0x20,
}
