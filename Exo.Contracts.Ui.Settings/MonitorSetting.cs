using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum MonitorSetting : uint
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	Brightness = 1,
	[EnumMember]
	Contrast = 2,
	[EnumMember]
	AudioVolume = 3,
}
