using System.IO.Pipes;

namespace Exo.Rpc;

public abstract class PipeClient
{
	internal abstract byte[] Buffers { get; }
	internal abstract CancellationToken CancellationToken { get; }
}

public class PipeClient<TConnection> : PipeClient, IAsyncDisposable
	where TConnection : PipeClientConnection, IPipeClientConnection<TConnection>
{
	private readonly byte[] _buffers;
	private readonly string _pipeName;
	private TConnection? _currentConnection;
	private readonly PipeTransmissionMode _transmissionMode;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _runTask;

	public PipeClient(string pipeName) : this(pipeName, PipeTransmissionMode.Message) { }

	public PipeClient(string pipeName, PipeTransmissionMode transmissionMode)
	{
		_buffers = GC.AllocateUninitializedArray<byte>(PipeConnection.ReadBufferSize + PipeConnection.WriteBufferSize, pinned: true);
		_pipeName = pipeName;
		_transmissionMode = transmissionMode;
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

	// Maybe we want to make this async at some point?
	protected TConnection? CurrentConnection => Volatile.Read(ref _currentConnection);

	internal override byte[] Buffers => _buffers;
	internal override CancellationToken CancellationToken => (Volatile.Read(ref _cancellationTokenSource) ?? throw new ObjectDisposedException(GetType().FullName)).Token;

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var stream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
					await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
					stream.ReadMode = _transmissionMode;
					try
					{
						Volatile.Write(ref _currentConnection, TConnection.Create(this, stream));
					}
					catch
					{
						stream.Dispose();
						throw;
					}
					try
					{
						await _currentConnection.StartAndGetRunTask().WaitAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
					{
						// TODO: Log ?
					}
				}
				finally
				{
					if (Interlocked.Exchange(ref _currentConnection, null) is { } connection) await connection.DisposeAsync();
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}
