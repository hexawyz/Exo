using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public enum BatteryStatus : byte
{
	[EnumMember]
	Unknown = 0,

	[EnumMember]
	Idle = 1,

	[EnumMember]
	Discharging = 2,

	[EnumMember]
	Charging = 3,
	[EnumMember]
	ChargingNearlyComplete = 4,
	[EnumMember]
	ChargingComplete = 5,

	[EnumMember]
	Error = 128,
	[EnumMember]
	TooHot = 129,
	[EnumMember]
	Missing = 130,
	[EnumMember]
	Invalid = 131,
}
