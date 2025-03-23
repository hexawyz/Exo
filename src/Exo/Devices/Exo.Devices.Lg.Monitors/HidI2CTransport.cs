using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;
using Exo.I2C;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Lg.Monitors;

public sealed class HidI2CTransport : II2cBus, IAsyncDisposable
{
	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private const int WriteStateReady = 0;
	private const int WriteStateReserved = 1;
	private const int WriteStateDisposed = -1;

	private sealed class ReadWaitState : TaskCompletionSource
	{
		public ReadWaitState(int sequenceNumber, Memory<byte> destination)
		{
			SequenceNumber = sequenceNumber;
			Destination = destination;
		}

		public int SequenceNumber { get; }
		public Memory<byte> Destination { get; }
	}

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private readonly byte _sessionId;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readAsyncTask;
	private ReadWaitState? _pendingOperation;
	private int _sequenceNumber;
	private readonly ILogger<HidI2CTransport> _logger;

	/// <summary>Creates a new instance of the class <see cref="HidI2CTransport"/>.</summary>
	/// <param name="stream">A stream to use for receiving and sending messages.</param>
	/// <param name="sessionId">A byte value to be used for identifying requests done by the instance.</param>
	/// <param name="cancellationToken"></param>
	public static async Task<HidI2CTransport> CreateAsync(HidFullDuplexStream stream, byte sessionId, ILogger<HidI2CTransport> logger, CancellationToken cancellationToken)
	{
		var transport = new HidI2CTransport(stream, logger, sessionId);
		try
		{
			await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return transport;
	}

	private HidI2CTransport(HidFullDuplexStream stream, ILogger<HidI2CTransport> logger, byte sessionId)
	{
		_stream = stream;
		_logger = logger;
		_sessionId = sessionId;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_buffers[65] = 0; // Zero-initialize the write report ID.
		_cancellationTokenSource = new CancellationTokenSource();
		_readAsyncTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _readAsyncTask.ConfigureAwait(false);
	}

	private byte BeginRequest()
	{
		int sequenceNumber = Volatile.Read(ref _sequenceNumber) & unchecked((int)0xFF000000U);
		while (true)
		{
			int oldState = Interlocked.CompareExchange(ref _sequenceNumber, sequenceNumber | WriteStateReserved, sequenceNumber | WriteStateReady);
			if (sequenceNumber != (sequenceNumber = oldState & unchecked((int)0xFF000000U)))
			{
				continue;
			}
			oldState &= 0xFFFFFF;
			if (oldState != WriteStateReady)
			{
				if (oldState == WriteStateDisposed) throw new ObjectDisposedException(nameof(HidI2CTransport));
				else throw new InvalidOperationException("An operation is already pending.");
			}
			return (byte)(sequenceNumber >>> 24);
		}
	}

	private void EndRequest(byte oldSequenceNumber) => EndRequest(oldSequenceNumber, (byte)(oldSequenceNumber + 1));

	private void EndRequest(byte oldSequenceNumber, byte newSequenceNumber)
		=> Interlocked.CompareExchange(ref _sequenceNumber, newSequenceNumber << 24 | WriteStateReady, oldSequenceNumber << 24 | WriteStateReserved);

	private async Task HandshakeAsync(CancellationToken cancellationToken)
	{
		byte sequenceNumber = BeginRequest();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteHandshakeRequest(buffer.Span[1..], _sessionId, sequenceNumber);
			var response = new byte[8];
			var waitState = new ReadWaitState(sequenceNumber, response);
			Volatile.Write(ref _pendingOperation, waitState);
			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				await waitState.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_pendingOperation = null;
			}

			if (!response.AsSpan().Slice(5, 3).SequenceEqual("HID"u8))
			{
				throw new InvalidDataException("Invalid handshake data received.");
			}
		}
		finally
		{
			EndRequest(sequenceNumber);
		}
	}

	public async ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
	{
		if (bytes.Length > 56)
		{
			throw new ArgumentException("Cannot write more than 56 bytes.");
		}

		byte sequenceNumber = BeginRequest();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteI2CWriteRequest(buffer.Span[1..], _sessionId, sequenceNumber, address, bytes.Span);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndRequest(sequenceNumber);
		}
	}

	public async ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
	{
		if (bytes.Length > 55)
		{
			throw new ArgumentException("Cannot write more than 55 bytes.");
		}

		byte sequenceNumber = BeginRequest();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteI2CWriteRequest(buffer.Span[1..], _sessionId, sequenceNumber, address, register, bytes.Span);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndRequest(sequenceNumber);
		}
	}

	public async ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
	{
		if (bytes.Length > 60)
		{
			// It might be possible to request more than 60 bytes and retrieve the results in multiple packets. (Up to 255 bytes over 5 packets ?)
			// But unless there is a need for this and more importantly, a way to try this, it is better to stay limited to the maximum possible in a single packet.
			throw new ArgumentException("Cannot request more than 60 bytes at once.");
		}

		byte sequenceNumber = BeginRequest();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteI2CReadRequestHeader(buffer.Span[1..], _sessionId, sequenceNumber, address, (byte)bytes.Length);
			var waitState = new ReadWaitState(sequenceNumber, bytes);
			Volatile.Write(ref _pendingOperation, waitState);
			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				await waitState.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_pendingOperation = null;
			}
		}
		finally
		{
			EndRequest(sequenceNumber);
		}
	}

	public async ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
	{
		if (bytes.Length > 60)
		{
			// It might be possible to request more than 60 bytes and retrieve the results in multiple packets. (Up to 255 bytes over 5 packets ?)
			// But unless there is a need for this and more importantly, a way to try this, it is better to stay limited to the maximum possible in a single packet.
			//throw new ArgumentException("Cannot request more than 60 bytes at once.");
		}

		byte sequenceNumber = BeginRequest();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteI2CReadRequest(buffer.Span[1..], _sessionId, sequenceNumber, address, register, (byte)bytes.Length);
			var waitState = new ReadWaitState(sequenceNumber, bytes);
			Volatile.Write(ref _pendingOperation, waitState);
			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				await waitState.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_pendingOperation = null;
			}
		}
		finally
		{
			EndRequest(sequenceNumber);
		}
	}

	private static void WriteHandshakeRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber)
	{
		buffer.Clear();

		// OnScreen Control software uses 0x0C here, but we only send 7 bytes, so it should be 7.
		buffer[0] = 0x07;
		buffer[1] = sequenceNumber;
		buffer[2] = sessionId;
		buffer[3] = 0x01;
		buffer[4] = 0x80;
		buffer[5] = 0x1a;
		buffer[6] = 0x06;
	}

	private static void WriteI2CWriteRequestHeader(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte length)
	{
		buffer[0] = 0x08;
		buffer[1] = sequenceNumber;
		buffer[2] = sessionId;
		buffer[3] = 0x03;
		buffer[4] = length;
		buffer[5] = 0;
		buffer[6] = 0x03;
		buffer[7] = deviceAddress;
	}

	private static void WriteI2CReadRequestHeader(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte length)
	{
		buffer[0] = 0x08;
		buffer[1] = sequenceNumber;
		buffer[2] = sessionId;
		buffer[3] = 0x04;
		buffer[4] = length;
		buffer[5] = 0;
		buffer[6] = 0x0b;
		buffer[7] = deviceAddress;
	}

	private static void WriteI2CWriteRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, ReadOnlySpan<byte> data)
	{
		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, (byte)data.Length);
		data.CopyTo(buffer[8..]);
	}

	private static void WriteI2CWriteRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte register, ReadOnlySpan<byte> data)
	{
		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, (byte)(data.Length + 1));
		buffer[8] = register;
		data.CopyTo(buffer[9..]);
	}

	private static void WriteI2CReadRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte register, byte length)
	{
		WriteI2CReadRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, length);
		buffer[8] = register;
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, 65);
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

				remaining = remaining.Slice(count);
			}
			while (remaining.Length != 0);

			ProcessReadMessage(buffer.Span[1..]);
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		if (message[2] != _sessionId) return;

		byte sequenceNumber = message[1];

		var pendingOperation = Volatile.Read(ref _pendingOperation);

		pendingOperation = sequenceNumber == pendingOperation?.SequenceNumber ? pendingOperation : null;

		int length = message[0] - 4;

		// The first byte in the raw HID response messages seem to always be the message data length. From what we can infer from the data, it needs to be at least 4 bytes.
		// TODO: Emit a log when there is an error and no state to bubble up the error.
		if (length < 0)
		{
			if (pendingOperation is not null)
			{
				pendingOperation.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidDataException("The received response has an invalid length.")));
			}
			else
			{
				_logger.HidI2CTransportMessageInvalidMessageDataLength();
			}
			return;
		}
		else if (message[3] != 0x00)
		{
			if (pendingOperation is not null)
			{
				pendingOperation.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidDataException("The received response contains an unexpected byte value.")));
			}
			else
			{
				_logger.HidI2CTransportMessageInvalidMessage();
			}
			return;
		}

		if (pendingOperation is not null)
		{
			if (pendingOperation.Destination.Length == length)
			{
				message[4..message[0]].CopyTo(pendingOperation.Destination.Span);
				pendingOperation.TrySetResult();
			}

			if (pendingOperation.Destination.Length < length)
			{
				pendingOperation.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The received response is longer that the provided buffer.")));
				return;
			}
			if (pendingOperation.Destination.Length > length)
			{
				pendingOperation.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The received response is smaller that the provided buffer.")));
				return;
			}
		}
	}
}
