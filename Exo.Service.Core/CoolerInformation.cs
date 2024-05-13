using Exo.Cooling;

namespace Exo.Service;

internal record struct CoolerInformation
{
	public CoolerInformation(Guid coolerId, Guid? coolerSensorId, CoolerType type, CoolingModes supportedCoolingModes)
	{
		CoolerId = coolerId;
		SpeedSensorId = coolerSensorId;
		Type = type;
		SupportedCoolingModes = supportedCoolingModes;
	}

	public Guid CoolerId { get; }
	public Guid? SpeedSensorId { get; }
	public CoolerType Type { get; }
	public CoolingModes SupportedCoolingModes { get; }
}
