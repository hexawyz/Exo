using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
using Exo.Contracts.Ui;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

namespace Exo.Ui;

public class ServiceConnectionManager : IAsyncDisposable
{
	private readonly string _pipeName;
	private readonly int _reconnectDelay;
	private readonly SocketsHttpHandler _handler;
	private TaskCompletionSource<GrpcChannel> _channelTaskCompletionSource;
	private readonly GrpcChannelOptions _grpcChannelOptions;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _runTask;

	public ServiceConnectionManager(string pipeName, int reconnectDelay)
	{
		ArgumentException.ThrowIfNullOrEmpty(pipeName);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reconnectDelay);
		_pipeName = pipeName;
		_reconnectDelay = reconnectDelay;
		_handler = new SocketsHttpHandler { ConnectCallback = (ctx, ct) => ConnectToPipeAsync(ct) };
		_grpcChannelOptions = new() { HttpHandler = _handler };
		_channelTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

		cts.Cancel();
		await _runTask.ConfigureAwait(false);
		cts.Dispose();
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				var channel = CreateChannel();
				try
				{
					var lifetimeService = channel.CreateGrpcService<IServiceLifetimeService>();
					if (await lifetimeService.TryGetVersionAsync(cancellationToken).ConfigureAwait(false) is not string version)
					{
						cancellationToken.ThrowIfCancellationRequested();
						break;
					}
					_channelTaskCompletionSource.TrySetResult(channel);
					using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
					{
						try
						{
							await OnConnectedAsync(channel, cts.Token).ConfigureAwait(false);
						}
						catch (Exception)
						{
							// TODO: Propagate exceptions somehow ?
						}
						try
						{
							await lifetimeService.WaitForStopAsync(cancellationToken).ConfigureAwait(false);
						}
						finally
						{
							Volatile.Write(ref _channelTaskCompletionSource, new(TaskCreationOptions.RunContinuationsAsynchronously));
						}
						cts.Cancel();
					}
				}
				catch (Exception)
				{
					// NB: Maybe this should be logged. The most important part for now is to not break the loop.
				}
				finally
				{
					try { await channel.ShutdownAsync().ConfigureAwait(false); }
					catch { }
					channel.Dispose();
				}
				try
				{
					await OnDisconnectedAsync().ConfigureAwait(false);
				}
				catch (Exception)
				{
					// TODO: Propagate exceptions somehow ?
				}
				await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			var objectDisposedException = ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(ServiceConnectionManager).FullName));
			if (!_channelTaskCompletionSource.TrySetException(objectDisposedException))
			{
				var disposedTaskCompletionSource = new TaskCompletionSource<GrpcChannel>();
				disposedTaskCompletionSource.TrySetException(objectDisposedException);
				Volatile.Write(ref _channelTaskCompletionSource, disposedTaskCompletionSource);
			}
		}
	}

	protected virtual Task OnConnectedAsync(GrpcChannel channel, CancellationToken disconnectionToken) => Task.CompletedTask;

	protected virtual Task OnDisconnectedAsync() => Task.CompletedTask;

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

	private GrpcChannel CreateChannel() => GrpcChannel.ForAddress("http://localhost", _grpcChannelOptions);

	public async ValueTask<TService> CreateServiceAsync<TService>(CancellationToken cancellationToken)
		where TService : class
	{
		await Task.Yield();
		return (await _channelTaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false)).CreateGrpcService<TService>();
	}
}
