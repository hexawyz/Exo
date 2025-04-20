namespace Exo.Cooling;

/// <summary>Identifies the type of a cooler.</summary>
/// <remarks>
/// When a cooler is a <see cref="CoolerType.Fan"/>, it could represent more than one fan.
/// This does, however, not impact the cooling API, so that information is left to be provided in separate metadata if it is necessary.
/// </remarks>
public enum CoolerType : byte
{
	Other = 0,
	Fan = 1,
	Pump = 2,
}
