using System.Runtime.Serialization;

namespace Exo.Features;

[DataContract]
public enum BatteryStatus : byte
{
	/// <summary>Indicates an unknown battery charging status.</summary>
	[EnumMember]
	Unknown = 0,

	/// <summary>Indicates an battery that is neither charging nor discharging.</summary>
	// Quite unlikely but we might need this status at some point?
	[EnumMember]
	Idle = 1,

	/// <summary>Indicates a battery that is discharging.</summary>
	/// <remarks>This is generally the case when the device is disconnected from power.</remarks>
	[EnumMember]
	Discharging = 2,

	/// <summary>Indicates a battery that is charging.</summary>
	[EnumMember]
	Charging = 3,
	/// <summary>Indicates a battery that is charging and close to completion.</summary>
	[EnumMember]
	ChargingNearlyComplete = 4,
	/// <summary>Indicates a battery that is completely charged.</summary>
	/// <remarks>Charge level of such a battery shall always be 100%, and it can be assumed as such in the code.</remarks>
	[EnumMember]
	ChargingComplete = 5,

	/// <summary>Indicates a non-specific battery-related error.</summary>
	[EnumMember]
	Error = 128,
	/// <summary>Indicates a battery that is too hot.</summary>
	[EnumMember]
	TooHot = 129,
	/// <summary>Indicates a missing battery.</summary>
	[EnumMember]
	Missing = 130,
	/// <summary>Indicates an invalid battery.</summary>
	[EnumMember]
	Invalid = 131,
}
