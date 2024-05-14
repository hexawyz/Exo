namespace Exo.Service;

public readonly struct CoolerPowerLimits(byte minimumPower, bool canSwitchOff)
{
	public byte MinimumPower { get; } = minimumPower;
	public bool CanSwitchOff { get; } = canSwitchOff;
}
