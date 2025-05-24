using System.IO.Pipes;
using Exo.Ipc;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Ipc;

internal sealed class HelperPipeServer : PipeServer<HelperPipeServerConnection>
{
	private readonly ILogger<HelperPipeServerConnection> _connectionLogger;
	internal OverlayNotificationService OverlayNotificationService { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal MonitorControlProxyService MonitorControlProxyService { get; }

	public HelperPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		ILogger<HelperPipeServerConnection> connectionLogger,
		OverlayNotificationService overlayNotificationService,
		CustomMenuService customMenuService,
		MonitorControlProxyService monitorControlProxyService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		_connectionLogger = connectionLogger;
		OverlayNotificationService = overlayNotificationService;
		CustomMenuService = customMenuService;
		MonitorControlProxyService = monitorControlProxyService;
	}

	protected override HelperPipeServerConnection CreateConnection(NamedPipeServerStream stream)
		=> new(_connectionLogger, this, stream);
}
