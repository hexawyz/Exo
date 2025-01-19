using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum MonitorShape : byte
{
	[EnumMember]
	Rectangle = 0,
	[EnumMember]
	Square = 1,
	[EnumMember]
	Circle = 2,
}
