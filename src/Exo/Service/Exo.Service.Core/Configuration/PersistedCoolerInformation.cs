using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Exo.Cooling;

namespace Exo.Service.Configuration;

[TypeId(0x74E0B7D0, 0x3CD7, 0x4B85, 0xA5, 0xD4, 0x5E, 0x5B, 0x38, 0xE8, 0xC6, 0xFC)]
internal readonly struct PersistedCoolerInformation
{
	public PersistedCoolerInformation(CoolerInformation info)
	{
		SensorId = info.SpeedSensorId;
		Type = info.Type;
		SupportedCoolingModes = info.SupportedCoolingModes;
		PowerLimits = info.PowerLimits;
		HardwareCurveInputSensorIds = info.HardwareCurveInputSensorIds;
	}

	[JsonConstructor]
	public PersistedCoolerInformation(Guid? sensorId, CoolerType type, CoolingModes supportedCoolingModes, CoolerPowerLimits? powerLimits, ImmutableArray<Guid> hardwareCurveInputSensorIds)
	{
		SensorId = sensorId;
		Type = type;
		SupportedCoolingModes = supportedCoolingModes;
		PowerLimits = powerLimits;
		HardwareCurveInputSensorIds = hardwareCurveInputSensorIds;
	}

	public Guid? SensorId { get; }
	public CoolerType Type { get; }
	public CoolingModes SupportedCoolingModes { get; }
	public CoolerPowerLimits? PowerLimits { get; }
	public ImmutableArray<Guid> HardwareCurveInputSensorIds { get; }
}
