using System.IO.Pipes;
using Exo.Ipc;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Exo.Service.Ipc;

internal sealed class ExoUiPipeClient : PipeClient<ExoUiPipeClientConnection>
{
	private readonly ILogger<ExoUiPipeClientConnection> _connectionLogger;
	private readonly DispatcherQueue _dispatcherQueue;
	private readonly IServiceClient _serviceClient;

	public ExoUiPipeClient
	(
		string pipeName,
		ILogger<ExoUiPipeClientConnection> connectionLogger,
		DispatcherQueue dispatcherQueue,
		IServiceClient serviceClient
	) : base(pipeName, PipeTransmissionMode.Message)
	{
		_connectionLogger = connectionLogger;
		_dispatcherQueue = dispatcherQueue;
		_serviceClient = serviceClient;
	}

	protected override ExoUiPipeClientConnection CreateConnection(NamedPipeClientStream stream)
		=> new(_connectionLogger, this, stream, _dispatcherQueue, _serviceClient);
}
