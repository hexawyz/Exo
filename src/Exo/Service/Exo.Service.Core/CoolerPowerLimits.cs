
using System.Text.Json.Serialization;

namespace Exo.Service;

[method: JsonConstructor]
public readonly struct CoolerPowerLimits(byte minimumPower, bool canSwitchOff) : IEquatable<CoolerPowerLimits>
{
	public byte MinimumPower { get; } = minimumPower;
	public bool CanSwitchOff { get; } = canSwitchOff;

	public override bool Equals(object? obj) => obj is CoolerPowerLimits limits && Equals(limits);
	public bool Equals(CoolerPowerLimits other) => MinimumPower == other.MinimumPower && CanSwitchOff == other.CanSwitchOff;
	public override int GetHashCode() => HashCode.Combine(MinimumPower, CanSwitchOff);

	public static bool operator ==(CoolerPowerLimits left, CoolerPowerLimits right) => left.Equals(right);
	public static bool operator !=(CoolerPowerLimits left, CoolerPowerLimits right) => !(left == right);
}
