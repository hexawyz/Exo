using System.IO.Pipes;
using System.Runtime.ExceptionServices;

namespace Exo.Rpc;

internal sealed class HelperRpcService : IHostedService
{
	private PipeServer<HelperPipeServerConnection>? _server;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_server is not null) return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		_server = new("Local\\Exo.Service.Helper", 2, PipeTransmissionMode.Message);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_server is not { } server) throw new InvalidOperationException();
		await server.DisposeAsync().ConfigureAwait(false);
	}
}
