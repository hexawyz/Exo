namespace Exo.Settings.Ui.Services;

internal interface IPowerService
{
	Task SetLowPowerModeBatteryThresholdAsync(Guid deviceId, Half batteryThreshold, CancellationToken cancellationToken);

	Task SetIdleSleepTimerAsync(Guid deviceId, TimeSpan idleTimer, CancellationToken cancellationToken);

	Task SetWirelessBrightnessAsync(Guid deviceId, byte brightness, CancellationToken cancellationToken);
}
