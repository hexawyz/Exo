using System.IO.Pipes;

namespace Exo.Ipc;

public abstract class PipeClient
{
	internal abstract byte[] Buffers { get; }
	internal abstract CancellationToken CancellationToken { get; }
}

public abstract class PipeClient<TConnection> : PipeClient, IAsyncDisposable
	where TConnection : PipeClientConnection
{
	private readonly byte[] _buffers;
	private readonly string _pipeName;
	private TConnection? _currentConnection;
	private readonly PipeTransmissionMode _transmissionMode;
	private CancellationTokenSource? _cancellationTokenSource;
	private Task _runTask;

#pragma warning disable CA1416 // Validate platform compatibility
	public PipeClient(string pipeName) : this(pipeName, PipeTransmissionMode.Message) { }
#pragma warning restore CA1416 // Validate platform compatibility

	public PipeClient(string pipeName, PipeTransmissionMode transmissionMode)
	{
		_buffers = GC.AllocateUninitializedArray<byte>(PipeConnection.ReadBufferSize + PipeConnection.WriteBufferSize, pinned: true);
		_pipeName = pipeName;
		_transmissionMode = transmissionMode;
		_cancellationTokenSource = new();
		_runTask = Task.CompletedTask;
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

	public async ValueTask StartAsync(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _cancellationTokenSource) is not { } cts) throw new ObjectDisposedException(GetType().FullName);
		if (!ReferenceEquals(Volatile.Read(ref _runTask), Task.CompletedTask)) throw new InvalidOperationException("The client was already started.");
		NamedPipeClientStream stream;
		using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token))
		{
			try
			{
				stream = await ConnectAsync(cts2.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					// When there is an OperationCanceledException, we assume that the state of the object has not been altered in a significantly negative way.
					// A subsequent StartAsync operation, although unlikely, should be able to succeed.
					throw;
				}
				else
				{
					// However, if the main cancellation token has been canceled, throw an ObjectDisposedException instead.
					throw new ObjectDisposedException(GetType().FullName);
				}
			}
			catch
			{
				// Dispose the instance if there was an exception, so that the Start method cannot be called in a loop.
				// This isn't high very quality, but if you are doing this, you are already using the class wrong anyway.
				_ = DisposeAsync();
				throw;
			}
		}
		_runTask = RunAsync(stream, cts.Token);
	}

	// This method will throw UnauthorizedAccessException if PipeOptions.FirstPipeInstance is used.
	// That way, we can propagate initialization problems to clients and avoid any problem.
	private NamedPipeClientStream CreateStream(PipeOptions options)
		=> new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, options);

	protected abstract TConnection CreateConnection(NamedPipeClientStream stream);

	// Maybe we want to make this async at some point?
	protected TConnection? CurrentConnection => Volatile.Read(ref _currentConnection);

	internal override byte[] Buffers => _buffers;
	internal override CancellationToken CancellationToken => (Volatile.Read(ref _cancellationTokenSource) ?? throw new ObjectDisposedException(GetType().FullName)).Token;

	private async Task RunAsync(NamedPipeClientStream? stream, CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					if (stream is null)
					{
						stream = await ConnectAsync(cancellationToken).ConfigureAwait(false);
					}
					try
					{
						Volatile.Write(ref _currentConnection, CreateConnection(stream));
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
					if (Interlocked.Exchange(ref _currentConnection, null) is { } connection) await connection.DisposeAsync();
					stream = null;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken)
	{
		var stream = CreateStream(PipeOptions.WriteThrough | PipeOptions.Asynchronous);
		try
		{
			await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
			stream.ReadMode = _transmissionMode;
		}
		catch
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return stream;
	}
}
