namespace Exo.Cooling;

/// <summary>Identifies the cooling mode of a cooler.</summary>
/// <remarks>
/// The members of this enumeration represent cooling modes commonly supported by devices.
/// More modes can be added in the future as necessary. (e.g. specific preset modes)
/// </remarks>
public enum CoolingMode : byte
{
	Automatic = 0,
	Manual = 1,
	HardwareControlCurve = 2,
}
