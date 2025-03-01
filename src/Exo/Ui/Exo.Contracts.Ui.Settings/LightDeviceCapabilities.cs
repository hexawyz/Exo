using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[Flags]
[DataContract]
public enum LightDeviceCapabilities : byte
{
	[EnumMember]
	None = 0b00000000,
	[EnumMember]
	Polled = 0b00000001,
}
