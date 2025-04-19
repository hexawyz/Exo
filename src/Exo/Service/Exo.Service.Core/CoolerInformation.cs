using System.Collections.Immutable;
using System.Linq;
using Exo.Cooling;

namespace Exo.Service;

internal readonly struct CoolerInformation(Guid coolerId, Guid? speedSensorId, CoolerType type, CoolingModes supportedCoolingModes, CoolerPowerLimits? powerLimits, ImmutableArray<Guid> hardwareCurveInputSensorIds) : IEquatable<CoolerInformation>
{
	public Guid CoolerId { get; } = coolerId;
	public Guid? SpeedSensorId { get; } = speedSensorId;
	public CoolerType Type { get; } = type;
	public CoolingModes SupportedCoolingModes { get; } = supportedCoolingModes;
	public CoolerPowerLimits? PowerLimits { get; } = powerLimits;
	public ImmutableArray<Guid> HardwareCurveInputSensorIds { get; } = hardwareCurveInputSensorIds;

	public override bool Equals(object? obj) => obj is CoolerInformation information && Equals(information);

	public bool Equals(CoolerInformation other)
		=> CoolerId == other.CoolerId &&
		SpeedSensorId == other.SpeedSensorId &&
		Type == other.Type &&
		SupportedCoolingModes == other.SupportedCoolingModes &&
		PowerLimits == other.PowerLimits &&
		(HardwareCurveInputSensorIds.IsDefaultOrEmpty ? other.HardwareCurveInputSensorIds.IsDefaultOrEmpty : !other.HardwareCurveInputSensorIds.IsDefaultOrEmpty && HardwareCurveInputSensorIds.SequenceEqual(other.HardwareCurveInputSensorIds));

	public override int GetHashCode()
		=> HashCode.Combine(CoolerId, SpeedSensorId, Type, SupportedCoolingModes, PowerLimits, HardwareCurveInputSensorIds.Length);

	public static bool operator ==(CoolerInformation left, CoolerInformation right) => left.Equals(right);
	public static bool operator !=(CoolerInformation left, CoolerInformation right) => !(left == right);
}
