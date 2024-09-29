using Exo.Features.Lighting;

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

/// <summary>This feature is exposed by devices that have a lighting brightness setting for wireless mode.</summary>
/// <remarks>This is related to <see cref="ILightingBrightnessFeature"/>.</remarks>
public interface IWirelessBrightnessFeature : IPowerManagementDeviceFeature
{
	/// <summary>Get the minimum brightness level.</summary>
	byte MinimumValue => 1;

	/// <summary>Get the maximum brightness level.</summary>
	/// <remarks>
	/// <para>
	/// Generally, devices will support setting 100 or 255 levels of brightness, but some devices may use more unusual values.
	/// Brightness values could always be abstracted to <c>100%</c> but it is more helpful to surface the ticks in the UI when possible.
	/// </para>
	/// </remarks>
	byte MaximumValue { get; }

	/// <summary>Gets the current maximum brightness brightness level.</summary>
	/// <remarks>The brightness value must be between <see cref="MinimumBrightness"/> and <see cref="MaximumBrightness"/> inclusive.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">The <paramref name="brightness"/> parameter is out of range.</exception>
	byte WirelessBrightness { get; }

	/// <summary>Sets the (maximum) brightness level of the device in wireless mode.</summary>
	/// <param name="maximumBrightness">The new maximum brightness.</param>
	/// <param name="cancellationToken"></param>
	Task SetWirelessBrightnessAsync(byte brightness, CancellationToken cancellationToken);
}
