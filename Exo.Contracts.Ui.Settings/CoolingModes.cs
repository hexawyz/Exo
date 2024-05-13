using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[Flags]
[DataContract]
public enum CoolingModes
{
	[EnumMember]
	None = 0x00,
	[EnumMember]
	Automatic = 0x01,
	[EnumMember]
	Manual = 0x02,
	[EnumMember]
	HardwareControlCurve = 0x04,
}
