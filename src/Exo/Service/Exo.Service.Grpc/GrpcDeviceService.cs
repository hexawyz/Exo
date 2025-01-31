using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcDeviceService : IDeviceService
{
	private readonly ConfigurationService _configurationService;
	private readonly ILogger<GrpcDeviceService> _logger;
	private readonly DeviceRegistry _driverRegistry;

	public GrpcDeviceService(ILogger<GrpcDeviceService> logger, ConfigurationService configurationService, DeviceRegistry driverRegistry, PowerService powerService)
	{
		_logger = logger;
		_configurationService = configurationService;
		_driverRegistry = driverRegistry;
	}

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
}
