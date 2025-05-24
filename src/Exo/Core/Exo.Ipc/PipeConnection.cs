using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Exo.Ipc;

/// <summary>Base implementation for pipe connections.</summary>
/// <remarks>
/// Derived classes are assumed to provide the write methods using the protected members exposed by the class.
/// All Write methods must:
/// 1. Be asynchronous
/// 2. Request a CancellationTokenSource for writing by calling CreateWriteCancellationTokenSource. (This assumes that most writes will be called by passing an external cancellation)
/// 3. Acquire the write lock
/// 4. Ideally use the pre-allocated write buffer (which is protected by the lock) to build messages
/// 5. Call the internal <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> method.
/// </remarks>
public abstract class PipeConnection : IAsyncDisposable
{
	// These default sizes can be tweaked later on.
	internal const int ReadBufferSize = 4096;
	internal const int WriteBufferSize = 4096;

	private readonly byte[] _buffers;
	private PipeStream? _stream;
	private readonly AsyncLock _lock;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly ILogger<PipeConnection> _logger;
	private Task? _runTask;

	private protected PipeConnection(ILogger<PipeConnection> logger, PipeStream stream, CancellationToken ownerCancellationToken)
		: this(logger, GC.AllocateUninitializedArray<byte>(ReadBufferSize + WriteBufferSize, pinned: true), stream, ownerCancellationToken)
	{
	}

	private protected PipeConnection(ILogger<PipeConnection> logger, byte[] buffers, PipeStream stream, CancellationToken ownerCancellationToken)
	{
		_logger = logger;
		_buffers = buffers;
		_stream = stream;
		_lock = new();
		_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ownerCancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _runTask!.ConfigureAwait(false);
			cts.Dispose();
			await OnDisposedAsync().ConfigureAwait(false);
		}
	}

	protected virtual ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

	private protected Memory<byte> ReadBuffer => MemoryMarshal.CreateFromPinnedArray(_buffers, 0, ReadBufferSize);
	protected Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_buffers, ReadBufferSize, WriteBufferSize);
	protected AsyncLock WriteLock => _lock;

	protected ILogger Logger => _logger;

	internal Task StartAndGetRunTask() => _runTask = RunAsync(_cancellationTokenSource!.Token);

	/// <summary>Provides the logic for running operations.</summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	internal async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				try
				{
					await ReadAndProcessMessagesAsync(_stream!, ReadBuffer, cancellationToken).ConfigureAwait(false);
				}
				catch (PipeClosedException)
				{
				}
				catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
				{
					_logger.PipeConnectionReadError(ex);
				}
			}
			finally
			{
				_stream!.Dispose();
				_stream = null;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	/// <summary>Read and processes messages from the stream until the end.</summary>
	/// <remarks>
	/// <para>
	/// Outside of the message parsing, implementations should only call the <see cref="PipeStream.ReadAsync(Memory{byte}, CancellationToken)"/> method on the stream.
	/// The <see cref="PipeStream.ReadAsync(Memory{byte}, CancellationToken)"/> method should be called at least once.
	/// When <see cref="PipeStream.ReadAsync(Memory{byte}, CancellationToken)"/> returns <c>0</c>, the method should stop all processing and return immediately.
	/// </para>
	/// <para>
	/// While implementations can use any buffer for reads, they should prefer using the provided read buffer, unless that is impossible for some reason.
	/// </para>
	/// </remarks>
	/// <param name="stream">The stream to use for reading.</param>
	/// <param name="readBuffer">The pre-allocated and reusable buffer to use for reads.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	protected abstract Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken);

	protected CancellationToken GetDefaultWriteCancellationToken()
	{
		if (TryGetDefaultWriteCancellationToken(out var cancellationToken)) return cancellationToken;
		throw new PipeClosedException();
	}

	protected bool TryGetDefaultWriteCancellationToken(out CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _cancellationTokenSource) is { } cts)
		{
			try
			{
				cancellationToken = cts.Token;
				return !cancellationToken.IsCancellationRequested;
			}
			catch (ObjectDisposedException)
			{
			}
		}
		cancellationToken = default;
		return false;
	}

	protected CancellationTokenSource CreateWriteCancellationTokenSource(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _cancellationTokenSource) is { } cts)
		{
			try
			{
				if (!cts.IsCancellationRequested) return CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
			}
			catch (ObjectDisposedException)
			{
			}
		}
		throw new PipeClosedException();
	}

	protected ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _stream) is not { } stream) throw new PipeClosedException();
		return stream.WriteAsync(buffer, cancellationToken);
	}
}
