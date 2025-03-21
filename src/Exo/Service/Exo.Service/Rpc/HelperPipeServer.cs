using System.IO.Pipes;
using Exo.Rpc;

namespace Exo.Service.Rpc;

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
