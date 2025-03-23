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
	private readonly PipeSecurity? _pipeSecurity;
	private readonly int _maxNumberOfServerInstances;
	private readonly PipeTransmissionMode _transmissionMode;
	private readonly AsyncLock _lock;
	private CancellationTokenSource? _cancellationTokenSource;
	private Task _runTask;

	public PipeServer(string pipeName) : this(pipeName, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, null) { }

	public PipeServer(string pipeName, PipeSecurity? pipeSecurity) : this(pipeName, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, pipeSecurity) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances) : this(pipeName, maxNumberOfServerInstances, PipeTransmissionMode.Message, null) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances, PipeSecurity? pipeSecurity) : this(pipeName, maxNumberOfServerInstances, PipeTransmissionMode.Message, pipeSecurity) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode) : this(pipeName, maxNumberOfServerInstances, transmissionMode, null) { }

	public PipeServer(string pipeName, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode, PipeSecurity? pipeSecurity)
	{
		_pipeName = pipeName;
		_pipeSecurity = pipeSecurity;
		_maxNumberOfServerInstances = maxNumberOfServerInstances;
		_transmissionMode = transmissionMode;
		_lock = new();
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

	public void Start()
	{
		if (Volatile.Read(ref _cancellationTokenSource) is not { } cts) throw new ObjectDisposedException(GetType().FullName);
		if (!ReferenceEquals(Volatile.Read(ref _runTask), Task.CompletedTask)) throw new InvalidOperationException("The server was already started.");
		try
		{
			_runTask = RunAsync(CreateStream(PipeOptions.WriteThrough | PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance), cts.Token);
		}
		catch
		{
			// Dispose the instance if there was an exception, so that the Start method cannot be called in a loop.
			// This isn't high very quality, but if you are doing this, you are already using the class wrong anyway.
			_ = DisposeAsync();
			throw;
		}
	}

	// This method will throw UnauthorizedAccessException if PipeOptions.FirstPipeInstance is used.
	// That way, we can propagate initialization problems to clients and avoid any problem.
	private NamedPipeServerStream CreateStream(PipeOptions options)
	{
		if (_pipeSecurity is not null)
		{
			return NamedPipeServerStreamAcl.Create
			(
				_pipeName,
				PipeDirection.InOut,
				NamedPipeServerStream.MaxAllowedServerInstances,
				_transmissionMode,
				options,
				0,
				0,
				_pipeSecurity
			);
		}
		else
		{
			return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, _transmissionMode, options);
		}
	}

	internal override CancellationToken CancellationToken => (Volatile.Read(ref _cancellationTokenSource) ?? throw new ObjectDisposedException(GetType().FullName)).Token;

	private async Task RunAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
	{
		try
		{
			if (!await ConnectAsync(stream, cancellationToken).ConfigureAwait(false)) return;

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				stream = CreateStream(PipeOptions.WriteThrough | PipeOptions.Asynchronous);
				if (!await ConnectAsync(stream, cancellationToken).ConfigureAwait(false)) return;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	private async ValueTask<bool> ConnectAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
	{
		try
		{
			await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (IOException ex)
		{
			// TODO: Log
			return true;
		}
		if (cancellationToken.IsCancellationRequested)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			return false;
		}
		AsyncLock.Registration lockRegistration;
		try
		{
			lockRegistration = await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
			return false;
		}

		try
		{
			_ = TConnection.Create(this, stream).StartAndGetRunTask();
		}
		catch (UnauthorizedAccessException)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
			await stream.DisposeAsync().ConfigureAwait(false);
			// TODO: Log if the access was not authorized.
			if (ex is not UnauthorizedAccessException)
			{
				throw;
			}
		}
		finally
		{
			lockRegistration.Dispose();
		}
		return true;
	}
}
