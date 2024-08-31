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
	/// <summary>Gets a value indicating the minimum idle time.</summary>
	TimeSpan MinimumIdleTime => TimeSpan.FromTicks(TimeSpan.TicksPerSecond);

	/// <summary>Gets a value indicating the maximum idle time.</summary>
	TimeSpan MaximumIdleTime { get; }

	/// <summary>Gets a value indicating the current idle time before device goes to sleep.</summary>
	TimeSpan IdleTime { get; }

	/// <summary>Sets the idle time before device goes to sleep.</summary>
	/// <param name="idleTime"></param>
	/// <param name="cancellationToken"></param>
	Task SetIdleTimeAsync(TimeSpan idleTime, CancellationToken cancellationToken);
}

/// <summary>This feature is exposed by devices that feature a low-power mode with a configurable battery threshold.</summary>
public interface ILowPowerModeBatteryThresholdFeature : IPowerManagementDeviceFeature
{
	/// <summary>Gets the current low power threshold.</summary>
	Half LowPowerThreshold { get; }

	/// <summary>Sets battery threshold below which the device will switch to low power mode.</summary>
	/// <remarks>The value should be between <c>0</c> and <c>100</c> included.</remarks>
	/// <param name="lowPowerThreshold">The new battery threshold.</param>
	/// <param name="cancellationToken"></param>
	Task SetLowPowerBatteryThresholdAsync(Half lowPowerThreshold, CancellationToken cancellationToken);
}
