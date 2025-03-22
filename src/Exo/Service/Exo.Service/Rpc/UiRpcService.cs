using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Exo.Service.Rpc;

internal sealed class UiRpcService : IHostedService
{
	private readonly ILogger<UiPipeServerConnection> _connectionLogger;
	private readonly CustomMenuService _customMenuService;
	private readonly SensorService _sensorService;

	public UiRpcService(ILogger<UiPipeServerConnection> connectionLogger, CustomMenuService customMenuService, SensorService sensorService)
	{
		_connectionLogger = connectionLogger;
		_customMenuService = customMenuService;
		_sensorService = sensorService;
	}

	private UiPipeServer? _server;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_server is not null) return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		var pipeSecurity = new PipeSecurity();

		SecurityIdentifier? currentUser;
		using (var currentIdentity = WindowsIdentity.GetCurrent())
		{
			currentUser = currentIdentity.Owner;
		}
		pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
		// Add the current user as explicit owner of the pipe if possible.
		// Otherwise, fallback to adding admin and system as owners. (We only need one of the two. Hopefully we will always know which is the current user.
		if (currentUser is not null)
		{
			pipeSecurity.AddAccessRule(new(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
		}
		else
		{
			// NB: The translation to NTAccount does not seem to be actually needed for any of those? Will fix later if this causes problems.
			//pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null)), PipeAccessRights.ReadWrite, AccessControlType.Allow));
			pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
			pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
		}
		_server = new("Local\\Exo.Service.Ui", pipeSecurity, _connectionLogger, _customMenuService, _sensorService);
		_server.Start();
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
