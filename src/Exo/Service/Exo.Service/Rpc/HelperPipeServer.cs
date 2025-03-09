using System.IO.Pipes;
using Exo.Service;

namespace Exo.Rpc;

internal sealed class HelperPipeServer : PipeServer<HelperPipeServerConnection>
{
	internal OverlayNotificationService OverlayNotificationService { get; }
	internal CustomMenuService CustomMenuService { get; }

	public HelperPipeServer(string pipeName, OverlayNotificationService overlayNotificationService, CustomMenuService customMenuService) : base(pipeName, 2, PipeTransmissionMode.Message)
	{
		OverlayNotificationService = overlayNotificationService;
		CustomMenuService = customMenuService;
	}
}
