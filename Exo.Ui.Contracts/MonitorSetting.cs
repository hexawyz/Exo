using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum MonitorSetting : uint
{
	[EnumMember]
	Unknown = 0,
	[EnumMember]
	Brightness = 1,
	[EnumMember]
	Contrast = 2,
}
