using System.Runtime.ExceptionServices;

namespace Exo.Service.Rpc;

internal sealed class HelperRpcService : IHostedService
{
	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly CustomMenuService _customMenuService;
	private readonly MonitorControlProxyService _monitorControlProxyService;

	public HelperRpcService(OverlayNotificationService overlayNotificationService, CustomMenuService customMenuService, MonitorControlProxyService monitorControlProxyService)
	{
		_overlayNotificationService = overlayNotificationService;
		_customMenuService = customMenuService;
		_monitorControlProxyService = monitorControlProxyService;
	}

	private HelperPipeServer? _server;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_server is not null) return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		_server = new("Local\\Exo.Service.Helper", _overlayNotificationService, _customMenuService, _monitorControlProxyService);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
