using System.Collections.Immutable;
using Exo.Cooling;

namespace Exo.Service;

internal record struct CoolerInformation
{
	public CoolerInformation(Guid coolerId, Guid? coolerSensorId, CoolerType type, CoolingModes supportedCoolingModes, CoolerPowerLimits? powerLimits, ImmutableArray<Guid> hardwareCurveInputSensorIds)
	{
		CoolerId = coolerId;
		SpeedSensorId = coolerSensorId;
		Type = type;
		SupportedCoolingModes = supportedCoolingModes;
		PowerLimits = powerLimits;
		HardwareCurveInputSensorIds = hardwareCurveInputSensorIds;
	}

	public Guid CoolerId { get; }
	public Guid? SpeedSensorId { get; }
	public CoolerType Type { get; }
	public CoolingModes SupportedCoolingModes { get; }
	public CoolerPowerLimits? PowerLimits { get; }
	public ImmutableArray<Guid> HardwareCurveInputSensorIds { get; }
}
