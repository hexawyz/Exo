namespace Exo.Features.PowerManagement;

/// <summary>Devices can allow access to their battery level by providing this feature.</summary>
public interface IBatteryStateDeviceFeature : IPowerManagementDeviceFeature
{
	/// <summary>This event is raised when the battery level of the device has changed.</summary>
	event Action<Driver, BatteryState> BatteryStateChanged;

	/// <summary>Gets the current battery level.</summary>
	BatteryState BatteryState { get; }
}

/// <summary>This feature is exposed by devices that can sleep when idle for some configurable amount of time.</summary>
public interface IIdleSleepTimerFeature : IPowerManagementDeviceFeature
{
	TimeSpan MinimumIdleTime { get; }
	TimeSpan MaximumIdleTime { get; }

	TimeSpan IdleTime { get; }

	Task SetIdleTimeAsync(TimeSpan idleTime, CancellationToken cancellationToken);
}

/// <summary>This feature is exposed by devices that feature a low-power mode with a configurable battery threshold.</summary>
public interface ILowPowerModeBatteryThresholdFeature : IPowerManagementDeviceFeature
{
	byte LowPowerThreshold { get; }

	Task SetLowPowerThresholdAsync(byte lowPowerThreshold, CancellationToken cancellationToken);
}
