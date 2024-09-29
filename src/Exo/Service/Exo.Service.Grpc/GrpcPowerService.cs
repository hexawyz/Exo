using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;
using GrpcPowerDeviceInformation = Exo.Contracts.Ui.Settings.PowerDeviceInformation;

namespace Exo.Service.Grpc;

internal sealed class GrpcPowerService : IPowerService
{
	private readonly PowerService _powerService;
	private readonly ILogger<GrpcPowerService> _logger;

	public GrpcPowerService(ILogger<GrpcPowerService> logger, PowerService powerService)
	{
		_powerService = powerService;
		_logger = logger;
	}

	public async IAsyncEnumerable<GrpcPowerDeviceInformation> WatchPowerDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var device in _powerService.WatchPowerDevicesAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return device.ToGrpc();
		}
	}

	public async IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcBatteryServiceWatchStart();
		try
		{
			await foreach (var notification in _powerService.WatchBatteryChangesAsync(cancellationToken))
			{
				if (_logger.IsEnabled(LogLevel.Trace))
				{
					_logger.GrpcBatteryServiceWatchNotification
					(
						notification.NotificationKind,
						notification.Key,
						notification.OldValue.Level,
						notification.NewValue.Level,
						notification.OldValue.BatteryStatus,
						notification.NewValue.BatteryStatus,
						notification.OldValue.ExternalPowerStatus,
						notification.NewValue.ExternalPowerStatus
					);
				}

				if (notification.NotificationKind == WatchNotificationKind.Removal) continue;

				yield return new()
				{
					DeviceId = notification.Key,
					Level = notification.NewValue.Level,
					BatteryStatus = (BatteryStatus)notification.NewValue.BatteryStatus,
					ExternalPowerStatus = (ExternalPowerStatus)notification.NewValue.ExternalPowerStatus
				};
			}
		}
		finally
		{
			_logger.GrpcBatteryServiceWatchStop();
		}
	}

	public async IAsyncEnumerable<PowerDeviceLowPowerModeBatteryThresholdUpdate> WatchLowPowerModeBatteryThresholdChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _powerService.WatchLowPowerBatteryThresholdChangesAsync(cancellationToken))
		{
			yield return new()
			{
				DeviceId = notification.DeviceId,
				BatteryThreshold = notification.BatteryThreshold,
			};
		}
	}

	public async IAsyncEnumerable<PowerDeviceIdleSleepTimerUpdate> WatchIdleSleepTimerChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _powerService.WatchIdleSleepTimerChangesAsync(cancellationToken))
		{
			yield return new()
			{
				DeviceId = notification.DeviceId,
				IdleTime = notification.IdleTime,
			};
		}
	}

	public async IAsyncEnumerable<PowerDeviceWirelessBrightnessUpdate> WatchWirelessBrightnessChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _powerService.WatchWirelessBrightnessChangesAsync(cancellationToken))
		{
			yield return new()
			{
				DeviceId = notification.DeviceId,
				Brightness = notification.Brightness,
			};
		}
	}

	public Task SetLowPowerModeBatteryThresholdAsync(PowerDeviceLowPowerModeBatteryThresholdUpdate request, CancellationToken cancellationToken)
		=> _powerService.SetLowPowerModeBatteryThresholdAsync(request.DeviceId, request.BatteryThreshold, cancellationToken);

	public Task SetIdleSleepTimerAsync(PowerDeviceIdleSleepTimerUpdate request, CancellationToken cancellationToken)
		=> _powerService.SetIdleSleepTimerAsync(request.DeviceId, request.IdleTime, cancellationToken);

	public Task SetWirelessBrightnessAsync(PowerDeviceWirelessBrightnessUpdate request, CancellationToken cancellationToken)
		=> _powerService.SetWirelessBrightnessAsync(request.DeviceId, request.Brightness, cancellationToken);
}
