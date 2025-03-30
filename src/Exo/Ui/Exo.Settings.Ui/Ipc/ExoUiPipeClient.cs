using System.IO.Pipes;
using Exo.Rpc;
using Microsoft.UI.Dispatching;

namespace Exo.Settings.Ui.Ipc;

internal sealed class ExoUiPipeClient : PipeClient<ExoUiPipeClientConnection>
{
	internal DispatcherQueue DispatcherQueue { get; }
	internal IServiceClient ServiceClient { get; }

	public ExoUiPipeClient
	(
		string pipeName,
		DispatcherQueue dispatcherQueue,
		IServiceClient serviceClient
	) : base(pipeName, PipeTransmissionMode.Message)
	{
		DispatcherQueue = dispatcherQueue;
		ServiceClient = serviceClient;
	}
}
