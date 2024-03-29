using System.IO.Pipes;
using System.Security.Principal;
using Grpc.Net.Client;

namespace Exo.Ui;

public sealed class ServiceConnectionManager : IDisposable
{
	private readonly string _pipeName;
	private readonly SocketsHttpHandler _handler;
	private readonly GrpcChannel _channel;

	public ServiceConnectionManager(string pipeName)
	{
		_pipeName = pipeName;
		_handler = new SocketsHttpHandler { ConnectCallback = (ctx, ct) => ConnectToPipeAsync(ct) };
		_channel = CreateChannel();
	}

	private ValueTask<Stream> ConnectToPipeAsync(CancellationToken cancellationToken) => ConnectToPipeAsync(_pipeName, cancellationToken);

	private static async ValueTask<Stream> ConnectToPipeAsync(string pipeName, CancellationToken cancellationToken)
	{
		var clientStream = new NamedPipeClientStream(
			serverName: ".",
			pipeName: pipeName,
			direction: PipeDirection.InOut,
			options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
			impersonationLevel: TokenImpersonationLevel.Anonymous);

		try
		{
			await clientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);
			return clientStream;
		}
		catch
		{
			clientStream.Dispose();
			throw;
		}
	}

	public GrpcChannel Channel => _channel;

	private GrpcChannel CreateChannel() => GrpcChannel.ForAddress("http://localhost", new() { HttpHandler = _handler });

	public void Dispose() => _channel.Dispose();
}
