using System.Runtime.ExceptionServices;
using Exo.Service;

namespace Exo.Rpc;

internal sealed class HelperRpcService : IHostedService
{
	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly CustomMenuService _customMenuService;

	public HelperRpcService(OverlayNotificationService overlayNotificationService, CustomMenuService customMenuService)
	{
		_overlayNotificationService = overlayNotificationService;
		_customMenuService = customMenuService;
	}

	private HelperPipeServer? _server;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_server is not null) return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		_server = new("Local\\Exo.Service.Helper", _overlayNotificationService, _customMenuService);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
