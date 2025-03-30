using System.IO.Pipes;
using Exo.Ipc;

namespace Exo.Service.Ipc;

internal sealed class HelperPipeServer : PipeServer<HelperPipeServerConnection>
{
	internal OverlayNotificationService OverlayNotificationService { get; }
	internal CustomMenuService CustomMenuService { get; }
	internal MonitorControlProxyService MonitorControlProxyService { get; }

	public HelperPipeServer
	(
		string pipeName,
		PipeSecurity? pipeSecurity,
		OverlayNotificationService overlayNotificationService,
		CustomMenuService customMenuService,
		MonitorControlProxyService monitorControlProxyService
	) : base(pipeName, 2, PipeTransmissionMode.Message, pipeSecurity)
	{
		OverlayNotificationService = overlayNotificationService;
		CustomMenuService = customMenuService;
		MonitorControlProxyService = monitorControlProxyService;
	}
}
