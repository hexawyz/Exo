using Exo.Cooling;

namespace Exo.Service;

internal record struct CoolerInformation
{
	public CoolerInformation(Guid coolerId, Guid? coolerSensorId, CoolerType type, CoolingModes supportedCoolingModes, CoolerPowerLimits? powerLimits)
	{
		CoolerId = coolerId;
		SpeedSensorId = coolerSensorId;
		Type = type;
		SupportedCoolingModes = supportedCoolingModes;
		PowerLimits = powerLimits;
	}

	public Guid CoolerId { get; }
	public Guid? SpeedSensorId { get; }
	public CoolerType Type { get; }
	public CoolingModes SupportedCoolingModes { get; }
	public CoolerPowerLimits? PowerLimits { get; }
}
