using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcMonitorService : IMonitorService
{
	private readonly MonitorService _monitorService;
	private readonly ILogger<GrpcMonitorService> _logger;

	public GrpcMonitorService(MonitorService monitorService, ILogger<GrpcMonitorService> logger)
	{
		_monitorService = monitorService;
		_logger = logger;
	}

	public async IAsyncEnumerable<MonitorSettingValue> WatchSettingsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcMonitorServiceSettingWatchStart();
		try
		{
			await foreach (var notification in _monitorService.WatchSettingsAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return new()
				{
					DeviceId = notification.DeviceId,
					Setting = notification.Setting.ToGrpc(),
					CurrentValue = notification.CurrentValue,
					MinimumValue = notification.MinimumValue,
					MaximumValue = notification.MaximumValue,
				};
			}
		}
		finally
		{
			_logger.GrpcMonitorServiceSettingWatchStop();
		}
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.MonitorInformation> WatchMonitorsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcMonitorServiceSettingWatchStart();
		try
		{
			await foreach (var notification in _monitorService.WatchMonitorsAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return notification.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcMonitorServiceSettingWatchStop();
		}
	}

	public ValueTask SetSettingValueAsync(MonitorSettingUpdate request, CancellationToken cancellationToken)
		=> _monitorService.SetSettingValueAsync(request.DeviceId, request.Setting.FromGrpc(), request.Value, cancellationToken);
}
