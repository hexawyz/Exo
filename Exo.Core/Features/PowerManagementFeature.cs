namespace Exo.Features.Cooling;

/// <summary>This feature is exposed by devices that can sleep when idle for some configurable amount of time.</summary>
public interface IIdleSleepTimerFeature : IPowerManagementDeviceFeature
{
}

/// <summary>This feature is exposed by devices that feature a low-power mode with a configurable battery threshold.</summary>
public interface ILowPowerModeBatteryThresholdFeature : IPowerManagementDeviceFeature
{
	
}

