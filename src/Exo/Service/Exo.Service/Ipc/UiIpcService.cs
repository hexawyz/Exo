using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Exo.Service.Ipc;

internal sealed class UiIpcService : IHostedService
{
	private readonly ILogger<UiPipeServerConnection> _connectionLogger;
	private readonly IAssemblyLoader _assemblyLoader;
	private readonly CustomMenuService _customMenuService;
	private readonly ImageStorageService _imageStorageService;
	private readonly DeviceRegistry _deviceRegistry;
	private readonly PowerService _powerService;
	private readonly MouseService _mouseService;
	private readonly MonitorService _monitorService;
	private readonly SensorService _sensorService;
	private readonly LightingEffectMetadataService _lightingEffectMetadataService;
	private readonly LightingService _lightingService;
	private readonly EmbeddedMonitorService _embeddedMonitorService;
	private readonly LightService _lightService;

	public UiIpcService
	(
		ILogger<UiPipeServerConnection> connectionLogger,
		IAssemblyLoader assemblyLoader,
		CustomMenuService customMenuService,
		ImageStorageService imageStorageService,
		DeviceRegistry deviceRegistry,
		PowerService powerService,
		MouseService mouseService,
		MonitorService monitorService,
		SensorService sensorService,
		LightingEffectMetadataService lightingEffectMetadataService,
		LightingService lightingService,
		EmbeddedMonitorService embeddedMonitorService,
		LightService lightService
	)
	{
		_connectionLogger = connectionLogger;
		_assemblyLoader = assemblyLoader;
		_customMenuService = customMenuService;
		_imageStorageService = imageStorageService;
		_deviceRegistry = deviceRegistry;
		_powerService = powerService;
		_mouseService = mouseService;
		_monitorService = monitorService;
		_sensorService = sensorService;
		_lightingEffectMetadataService = lightingEffectMetadataService;
		_lightingService = lightingService;
		_embeddedMonitorService = embeddedMonitorService;
		_lightService = lightService;
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
		_server = new
		(
			"Local\\Exo.Service.Ui",
			pipeSecurity,
			_connectionLogger,
			_assemblyLoader,
			_customMenuService,
			_imageStorageService,
			_deviceRegistry,
			_powerService,
			_mouseService,
			_monitorService,
			_sensorService,
			_lightingEffectMetadataService,
			_lightingService,
			_embeddedMonitorService,
			_lightService
		);
		_server.Start();
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
