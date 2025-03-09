using System.IO.Pipes;

namespace Exo.Rpc;

public abstract class PipeServer
{
	internal abstract CancellationToken CancellationToken { get; }
}

public class PipeServer<TConnection> : PipeServer, IAsyncDisposable
	where TConnection : PipeServerConnection, IPipeServerConnection<TConnection>
{
	private readonly string _pipeName;
	private readonly int _maxNumberOfServerInstances;
	private readonly PipeTransmissionMode _transmissionMode;
	private readonly AsyncLock _lock;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _runTask;

	public PipeServer(string pipeName) : this(pipeName, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances) : this(pipeName, maxNumberOfServerInstances, PipeTransmissionMode.Message) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode)
	{
		_pipeName = pipeName;
		_maxNumberOfServerInstances = maxNumberOfServerInstances;
		_transmissionMode = transmissionMode;
		_lock = new();
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _runTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	internal override CancellationToken CancellationToken => (Volatile.Read(ref _cancellationTokenSource) ?? throw new ObjectDisposedException(GetType().FullName)).Token;

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				NamedPipeServerStream stream;
				try
				{
					stream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxNumberOfServerInstances, _transmissionMode, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
					await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (IOException)
				{
					// TODO: Log
					continue;
				}
				if (cancellationToken.IsCancellationRequested)
				{
					await stream.DisposeAsync().ConfigureAwait(false);
					return;
				}
				IDisposable lockRegistration;
				try
				{
					lockRegistration = await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					await stream.DisposeAsync().ConfigureAwait(false);
					return;
				}

				try
				{
					_ = TConnection.Create(this, stream).StartAndGetRunTask();
				}
				catch (Exception ex)
				{
					// TODO: Log
					await stream.DisposeAsync().ConfigureAwait(false);
					throw;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}
