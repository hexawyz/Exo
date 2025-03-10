using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;

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
		var pipeSecurity = new PipeSecurity();
		pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
		// NB: The translation to NTAccount does not seem to be actually needed for any of those? Will fix later if this causes problems.
		//pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null)), PipeAccessRights.ReadWrite, AccessControlType.Allow));
		pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
		pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
		_server = new("Local\\Exo.Service.Helper", pipeSecurity, _overlayNotificationService, _customMenuService, _monitorControlProxyService);
		_server.Start();
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
