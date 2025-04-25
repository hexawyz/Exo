using System.IO.Pipes;
using Exo.Ipc;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Exo.Service.Ipc;

internal sealed class ExoUiPipeClient : PipeClient<ExoUiPipeClientConnection>
{
	internal ILogger<ExoUiPipeClientConnection> ConnectionLogger { get; }
	internal DispatcherQueue DispatcherQueue { get; }
	internal IServiceClient ServiceClient { get; }

	public ExoUiPipeClient
	(
		string pipeName,
		ILogger<ExoUiPipeClientConnection> connectionLogger,
		DispatcherQueue dispatcherQueue,
		IServiceClient serviceClient
	) : base(pipeName, PipeTransmissionMode.Message)
	{
		ConnectionLogger = connectionLogger;
		DispatcherQueue = dispatcherQueue;
		ServiceClient = serviceClient;
	}
}
