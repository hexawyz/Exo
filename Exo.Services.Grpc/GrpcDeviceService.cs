using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcDeviceService : IDeviceService, IAsyncDisposable
{
	private readonly ConfigurationService _configurationService;
	private readonly ILogger<GrpcDeviceService> _logger;
	private readonly DeviceRegistry _driverRegistry;
	private readonly PowerService _powerService;

	public GrpcDeviceService(ILogger<GrpcDeviceService> logger, ConfigurationService configurationService, DeviceRegistry driverRegistry, PowerService powerService)
	{
		_logger = logger;
		_configurationService = configurationService;
		_driverRegistry = driverRegistry;
		_powerService = powerService;
	}

	public ValueTask DisposeAsync() => _powerService.DisposeAsync();

	public async IAsyncEnumerable<WatchNotification<Contracts.Ui.Settings.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcDeviceServiceWatchStart();
		try
		{
			await foreach (var notification in _driverRegistry.WatchAllAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_logger.IsEnabled(LogLevel.Trace))
				{
					_logger.GrpcDeviceServiceWatchNotification(notification.Kind, notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, notification.DeviceInformation.IsAvailable);
				}
				yield return new()
				{
					NotificationKind = notification.Kind.ToGrpc(),
					Details = notification.DeviceInformation.ToGrpc(),
				};
			}
		}
		finally
		{
			_logger.GrpcDeviceServiceWatchStop();
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
}
