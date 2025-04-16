using System.Runtime.CompilerServices;
using System.ServiceModel;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Power")]
public interface IPowerService
{
	[OperationContract(Name = "WatchBatteryChanges")]
	IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchLowPowerBatteryThresholdChanges")]
	IAsyncEnumerable<PowerDeviceLowPowerModeBatteryThresholdUpdate> WatchLowPowerModeBatteryThresholdChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchIdleSleepTimerChanges")]
	IAsyncEnumerable<PowerDeviceIdleSleepTimerUpdate> WatchIdleSleepTimerChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "WatchWirelessBrightnessChanges")]
	IAsyncEnumerable<PowerDeviceWirelessBrightnessUpdate> WatchWirelessBrightnessChangesAsync(CancellationToken cancellationToken);

	[OperationContract(Name = "SetLowPowerBatteryThreshold")]
	Task SetLowPowerModeBatteryThresholdAsync(PowerDeviceLowPowerModeBatteryThresholdUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetIdleSleepTimer")]
	Task SetIdleSleepTimerAsync(PowerDeviceIdleSleepTimerUpdate request, CancellationToken cancellationToken);

	[OperationContract(Name = "SetWirelessBrightness")]
	Task SetWirelessBrightnessAsync(PowerDeviceWirelessBrightnessUpdate request, CancellationToken cancellationToken);
}

