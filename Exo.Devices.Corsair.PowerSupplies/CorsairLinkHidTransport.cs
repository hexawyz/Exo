using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Numerics;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Corsair.PowerSupplies;

// NB: This likely not the V1 protocol, but it is the one for HX1200i & similar devices.
internal sealed class CorsairLinkHidTransport : IAsyncDisposable
{
	private interface IPendingCommand
	{
		void WriteRequest(Span<byte> buffer);
		void ProcessResponse(ReadOnlySpan<byte> buffer);
		Task WaitAsync(CancellationToken cancellationToken);
		void Cancel();
	}

	private interface IPendingCommand<T> : IPendingCommand
	{
		new Task<T> WaitAsync(CancellationToken cancellationToken);
	}

	private abstract class WriteCommand : TaskCompletionSource, IPendingCommand
	{
		public abstract void WriteRequest(Span<byte> buffer);
		public abstract void ProcessResponse(ReadOnlySpan<byte> buffer);

		public Task WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);
		public void Cancel() => TrySetCanceled();
	}

	private sealed class ByteWriteCommand : WriteCommand
	{
		private readonly byte _command;
		private readonly byte _value;

		public ByteWriteCommand(byte command, byte value)
		{
			_command = command;
			_value = value;
		}

		public override void WriteRequest(Span<byte> buffer)
		{
			buffer[0] = 0x02;
			buffer[1] = _command;
			buffer[2] = _value;
		}

		public override void ProcessResponse(ReadOnlySpan<byte> buffer)
		{
			// NB: Not entirely sure about the protocol in case of error here.
			// e.g. What is returned if we send an inappropriate value?
			if (buffer[0] == 2)
			{
				byte command = buffer[1];
				if (command == _command)
				{
					if (buffer[2] == _value)
					{
						TrySetResult();
					}
				}
				else if (command == 0x00)
				{
					// If a command is sent to an invalid endpoint, the response will have command ID 0, which can also be "PAGE".
					// When running concurrently with other software, and even though we acquire a global lock,
					// it is possible that we intercept a valid response from the PAGE command because our read buffer is late for some reason.
					// Sadly, there isn't really a simple way to guarantee that the read buffer would be empty, but it is better to throw than leaving a task waiting eternally.
					TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new CorsairLinkWriteErrorException()));
				}
			}
		}
	}

	private abstract class ResultCommand<T> : TaskCompletionSource<T>, IPendingCommand<T>
	{
		public ResultCommand() { }

		public abstract void WriteRequest(Span<byte> buffer);
		public abstract void ProcessResponse(ReadOnlySpan<byte> buffer);

		Task IPendingCommand.WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);
		public Task<T> WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);

		public void Cancel() => TrySetCanceled();
	}

	private sealed class HandshakeCommand : ResultCommand<string>
	{
		public HandshakeCommand() { }

		public override void WriteRequest(Span<byte> buffer)
		{
			buffer[0] = 0xFE;
			buffer[1] = 0x03;
		}

		public override void ProcessResponse(ReadOnlySpan<byte> buffer)
		{
			if (buffer[0] == 0xFE && buffer[1] == 0x03)
			{
				var data = buffer[2..];
				int endIndex = data.IndexOf((byte)0);
				endIndex = endIndex < 0 ? data.Length : endIndex;
				TrySetResult(Encoding.UTF8.GetString(data[..endIndex]));
			}
		}
	}

	private abstract class ReadCommand<T> : ResultCommand<T>
	{
		private readonly byte _command;

		public ReadCommand(byte command) => _command = command;

		public sealed override void WriteRequest(Span<byte> buffer)
		{
			buffer[0] = 0x03;
			buffer[1] = _command;
		}

		public sealed override void ProcessResponse(ReadOnlySpan<byte> buffer)
		{
			if (buffer[0] == 3)
			{
				byte command = buffer[1];
				if (command == _command)
				{
					TrySetResult(ParseResult(buffer[2..]));
				}
				else if (command == 0x00)
				{
					// If a command is sent to an invalid endpoint, the response will have command ID 0, which can also be "PAGE".
					// When running concurrently with other software, and even though we acquire a global lock,
					// it is possible that we intercept a valid response from the PAGE command because our read buffer is late for some reason.
					// Sadly, there isn't really a simple way to guarantee that the read buffer would be empty, but it is better to throw than leaving a task waiting eternally.
					TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new CorsairLinkReadErrorException()));
				}
			}
		}

		protected abstract T ParseResult(ReadOnlySpan<byte> data);
	}

	private sealed class StringReadCommand : ReadCommand<string>
	{
		public StringReadCommand(byte command) : base(command) { }

		protected override string ParseResult(ReadOnlySpan<byte> data)
		{
			int endIndex = data.IndexOf((byte)0);
			endIndex = endIndex < 0 ? data.Length : endIndex;
			return Encoding.UTF8.GetString(data[..endIndex]);
		}
	}

	private sealed class ByteReadCommand : ReadCommand<byte>
	{
		public ByteReadCommand(byte command) : base(command) { }

		protected override byte ParseResult(ReadOnlySpan<byte> data) => data[0];
	}

	private sealed class Linear11ReadCommand : ReadCommand<Linear11>
	{
		public Linear11ReadCommand(byte command) : base(command) { }

		protected override Linear11 ParseResult(ReadOnlySpan<byte> data) => Linear11.FromRawValue(LittleEndian.ReadUInt16(data[0]));
	}

	public static async ValueTask<CorsairLinkHidTransport> CreateAsync(ILogger<CorsairLinkHidTransport> logger, HidFullDuplexStream stream, CancellationToken cancellationToken)
	{
		var transport = new CorsairLinkHidTransport(logger, stream);
		try
		{
			transport._handshakeDeviceName = await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return transport;
	}

	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private static readonly object DisposedSentinel = new();

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private object? _currentWaitState;
	private readonly ILogger<CorsairLinkHidTransport> _logger;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _readTask;
	private string? _handshakeDeviceName;

	private CorsairLinkHidTransport(ILogger<CorsairLinkHidTransport> logger, HidFullDuplexStream stream)
	{
		_stream = stream;
		_logger = logger;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_buffers[65] = 0; // Zero-initialize the write report ID.
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			if (Interlocked.Exchange(ref _currentWaitState, DisposedSentinel) is IPendingCommand pendingCommand)
			{
				pendingCommand.Cancel();
			}
			await _readTask.ConfigureAwait(false);
			_stream.Dispose();
			cts.Dispose();
		}
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		// TODO: See if it is easy/reasonable to add some synchronization between command writes and reads.
		// We still have to continuously read the messages, but there could be a way to register a pending command with read-side cooperation?
		// Something like: Always process reads immediately, but as soon as there is a pending operation, allow a write to be registered, and associate the next reads with it.
		// Not even sure it such a thing would be enough, though. (And it might be overkill)
		// It would be simpler if the protocol included request IDs.
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, MessageLength);
			while (true)
			{
				// Data is received in fixed length packets, so we expect to always receive exactly the number of bytes that the buffer can hold.
				var remaining = buffer;
				do
				{
					int count;
					try
					{
						count = await _stream.ReadAsync(remaining, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}

					if (count == 0)
					{
						return;
					}

					remaining = remaining[count..];
				}
				while (remaining.Length != 0);

				(Volatile.Read(ref _currentWaitState) as IPendingCommand)?.ProcessResponse(buffer.Span[1..]);
			}
		}
		catch
		{
			// TODO: Log
		}
	}
	private async ValueTask ExecuteCommandAsync(IPendingCommand waitState, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _currentWaitState, waitState, null) is { } oldState)
		{
			ObjectDisposedException.ThrowIf(ReferenceEquals(oldState, DisposedSentinel), typeof(CorsairLinkHidTransport));
			throw new InvalidOperationException("An operation is already running.");
		}

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		try
		{
			waitState.WriteRequest(buffer.Span[1..]);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			await waitState.WaitAsync(cancellationToken).ConfigureAwait(false);
			return;
		}
		finally
		{
			Interlocked.CompareExchange(ref _currentWaitState, null, waitState);
		}
	}

	private async ValueTask<T> ExecuteCommandAsync<T>(IPendingCommand<T> waitState, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _currentWaitState, waitState, null) is { } oldState)
		{
			ObjectDisposedException.ThrowIf(ReferenceEquals(oldState, DisposedSentinel), typeof(CorsairLinkHidTransport));
			throw new InvalidOperationException("An operation is already running.");
		}

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		try
		{
			waitState.WriteRequest(buffer.Span[1..]);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			return await waitState.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Interlocked.CompareExchange(ref _currentWaitState, null, waitState);
		}
	}

	private ValueTask<string> HandshakeAsync(CancellationToken cancellationToken) => ExecuteCommandAsync(new HandshakeCommand(), cancellationToken);

	public ValueTask<byte> ReadByteAsync(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new ByteReadCommand(command), cancellationToken);

	public ValueTask<Linear11> ReadLinear11Async(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new Linear11ReadCommand(command), cancellationToken);

	public ValueTask<string> ReadStringAsync(byte command, CancellationToken cancellationToken) => ExecuteCommandAsync(new StringReadCommand(command), cancellationToken);

	public ValueTask WriteByteAsync(byte command, byte value, CancellationToken cancellationToken) => ExecuteCommandAsync(new ByteWriteCommand(command, value), cancellationToken);
}
