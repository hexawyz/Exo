using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcEmbeddedMonitorService : IEmbeddedMonitorService
{
	private readonly ILogger<GrpcDeviceService> _logger;
	private readonly EmbeddedMonitorService _embeddedMonitorService;

	public GrpcEmbeddedMonitorService(ILogger<GrpcDeviceService> logger, EmbeddedMonitorService embeddedMonitorService)
	{
		_logger = logger;
		_embeddedMonitorService = embeddedMonitorService;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.EmbeddedMonitorDeviceInformation> WatchEmbeddedMonitorDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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

	public async IAsyncEnumerable<EmbeddedMonitorConfigurationUpdate> WatchConfigurationUpdatesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// TODO: Logging
		//_logger.GrpcSpecializedDeviceServiceWatchStart(GrpcService.EmbeddedMonitor);
		try
		{
			await foreach (var configuration in _embeddedMonitorService.WatchConfigurationChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return configuration.ToGrpc();
			}
		}
		finally
		{
			//_logger.GrpcSpecializedDeviceServiceWatchStop(GrpcService.EmbeddedMonitor);
		}
	}

	public async ValueTask SetBuiltInGraphicsAsync(EmbeddedMonitorSetBuiltInGraphicsRequest request, CancellationToken cancellationToken)
	{
		await _embeddedMonitorService.SetBuiltInGraphicsAsync(request.DeviceId, request.MonitorId, request.GraphicsId, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask SetImageAsync(EmbeddedMonitorSetImageRequest request, CancellationToken cancellationToken)
	{
		await _embeddedMonitorService.SetImageAsync(request.DeviceId, request.MonitorId, request.ImageId, request.CropRegion.FromGrpc(), cancellationToken).ConfigureAwait(false);
	}
}
