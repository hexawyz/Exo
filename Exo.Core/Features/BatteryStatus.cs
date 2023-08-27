namespace Exo.Features;

public enum BatteryStatus : byte
{
	/// <summary>Indicates an unknown battery charging status.</summary>
	Unknown = 0,

	/// <summary>Indicates an battery that is neither charging nor discharging.</summary>
	// Quite unlikely but we might need this status at some point?
	Idle = 1,

	/// <summary>Indicates a battery that is discharging.</summary>
	/// <remarks>This is generally the case when the device is disconnected from power.</remarks>
	Discharging = 2,

	/// <summary>Indicates a battery that is charging.</summary>
	Charging = 3,
	/// <summary>Indicates a battery that is charging and close to completion.</summary>
	ChargingNearlyComplete = 4,
	/// <summary>Indicates a battery that is completely charged.</summary>
	/// <remarks>Charge level of such a battery shall always be 100%, and it can be assumed as such in the code.</remarks>
	ChargingComplete = 5,

	/// <summary>Indicates a non-specific battery-related error.</summary>
	Error = 128,
	/// <summary>Indicates a battery that is too hot.</summary>
	TooHot = 129,
	/// <summary>Indicates a missing battery.</summary>
	Missing = 130,
	/// <summary>Indicates an invalid battery.</summary>
	Invalid = 131,
}
