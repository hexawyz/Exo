using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class BatteryChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required float? Level { get; init; }

	[DataMember(Order = 3)]
	public required BatteryStatus BatteryStatus { get; init; }

	[DataMember(Order = 4)]
	public required ExternalPowerStatus ExternalPowerStatus { get; init; }
}

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

[DataContract]
public enum ExternalPowerStatus : byte
{
	[EnumMember]
	IsDisconnected = 0,
	[EnumMember]
	IsConnected = 1,
	[EnumMember]
	IsSlowCharger = 2,
}
