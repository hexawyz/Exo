using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcEmbeddedMonitorService : IEmbeddedMonitorService
{
	private readonly ConfigurationService _configurationService;
	private readonly ILogger<GrpcDeviceService> _logger;
	private readonly EmbeddedMonitorService _embeddedMonitorService;

	public GrpcEmbeddedMonitorService(ILogger<GrpcDeviceService> logger, ConfigurationService configurationService, EmbeddedMonitorService embeddedMonitorService)
	{
		_logger = logger;
		_configurationService = configurationService;
		_embeddedMonitorService = embeddedMonitorService;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.EmbeddedMonitorDeviceInformation> WatchEmbeddedMonitorDevicesAsync(CancellationToken cancellationToken)
	{
		_logger.GrpcSpecializedDeviceServiceWatchStart(GrpcService.EmbeddedMonitor);
		try
		{
			await foreach (var notification in _embeddedMonitorService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return notification.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcSpecializedDeviceServiceWatchStop(GrpcService.EmbeddedMonitor);
		}
	}
}
