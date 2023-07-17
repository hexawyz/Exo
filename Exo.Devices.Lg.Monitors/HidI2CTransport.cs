using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools.DisplayDevices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Lg.Monitors;

public sealed class HidI2CTransport : IAsyncDisposable
{
	public const int DefaultDdcDeviceAddress = 0x37;

	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private const int WriteStateReady = 0;
	private const int WriteStateReserved = 1;
	private const int WriteStateDisposed = -1;

	private abstract class ResponseWaitState
	{
		private TaskCompletionSource _taskCompletionSource = new();

		public TaskCompletionSource TaskCompletionSource => Volatile.Read(ref _taskCompletionSource);

		public abstract void OnDataReceived(ReadOnlySpan<byte> message);

		[DebuggerHidden]
		[DebuggerStepThrough]
		[StackTraceHidden]
		public void SetNewException(Exception exception) => SetException(ExceptionDispatchInfo.SetCurrentStackTrace(exception));

		private void SetException(Exception exception) => TaskCompletionSource.TrySetException(exception);

		public void Reset()
		{
			TaskCompletionSource.TrySetCanceled();
			Volatile.Write(ref _taskCompletionSource, new());
		}
	}

	private sealed class HandshakeResponseWaitState : ResponseWaitState
	{
		public override void OnDataReceived(ReadOnlySpan<byte> message)
		{
			if (message.Slice(5, 3).SequenceEqual("HID"u8))
			{
				TaskCompletionSource.TrySetResult();
			}
			else
			{
				SetNewException(new InvalidDataException("Invalid handshake data received."));
			}
		}
	}

	private abstract class DdcResponseWaitState : ResponseWaitState
	{
		protected abstract byte DdcOpCode { get; }

		public sealed override void OnDataReceived(ReadOnlySpan<byte> message)
		{
			// DDC responses need to be at least 3 bytes long, as we need at least 0x6E + Length + Checksum
			if (message.Length < 3)
			{
				SetNewException(new InvalidDataException("The received response is not long enough."));
			}

			byte length;

			if (message[0] != 0x6E)
			{
				SetNewException(new InvalidDataException("The received response has an unexpected destination address."));
				return;
			}

			if (message[1] >= 0x81)
			{
				length = (byte)(message[1] & 0x7F);
			}
			else
			{
				SetNewException(new InvalidDataException("The received response has an invalid length."));
				return;
			}

			if (message[2] != DdcOpCode)
			{
				SetNewException(new InvalidDataException("The received response is referencing the wrong DDC opcode."));
				return;
			}

			if (DdcChecksum(message[..(length + 3)], 0x50) != 0)
			{
				SetNewException(new InvalidDataException("The received response has an invalid DDC checksum."));
				return;
			}

			ProcessDdcResponseContents(message.Slice(3, length - 1));
		}

		protected abstract void ProcessDdcResponseContents(ReadOnlySpan<byte> contents);
	}

	private abstract class VcpVariableLengthResponseWaitState : DdcResponseWaitState
	{
		public Memory<byte> Buffer { get; }
		private ushort _readLength;
		public ushort ReadLength => _readLength;
		private bool _isCompleted;
		public bool IsCompleted => _isCompleted;

		public VcpVariableLengthResponseWaitState(Memory<byte> buffer) => Buffer = buffer;

		protected override void ProcessDdcResponseContents(ReadOnlySpan<byte> contents)
		{
			ushort offset = (ushort)(contents[0] << 8 | contents[1]);
			var data = contents[2..];

			if (offset != _readLength)
			{
				SetNewException(new InvalidDataException("Non consecutive data packets were received."));
				return;
			}

			if (data.Length == 0)
			{
				_isCompleted = true;
				TaskCompletionSource.TrySetResult();
				return;
			}

			int nextLength = _readLength + data.Length;

			if (nextLength > 0xFFFF)
			{
				SetNewException(new InvalidDataException("The data exceeded the maximum size."));
				return;
			}

			var remaining = Buffer.Span[_readLength..];

			if (remaining.Length < data.Length)
			{
				data[..remaining.Length].CopyTo(remaining);
				SetNewException(new InvalidOperationException("The provided buffer is too small."));
			}
			else
			{
				data.CopyTo(remaining);
			}

			_readLength = (ushort)nextLength;

			TaskCompletionSource.TrySetResult();
		}
	}

	private sealed class DdcGetCapabilitiesWaitState : VcpVariableLengthResponseWaitState
	{
		protected override byte DdcOpCode => (byte)DdcCiCommand.CapabilitiesReply;

		public DdcGetCapabilitiesWaitState(Memory<byte> buffer) : base(buffer) { }
	}

	private sealed class VcpReadTableWaitState : VcpVariableLengthResponseWaitState
	{
		protected override byte DdcOpCode => (byte)DdcCiCommand.TableReadReply;

		public VcpReadTableWaitState(Memory<byte> buffer) : base(buffer) { }
	}

	private sealed class VcpGetResponseWaitState : DdcResponseWaitState
	{
		public byte VcpCode { get; }
		protected override byte DdcOpCode => (byte)DdcCiCommand.VcpReply;

		private ushort _currentValue;
		private ushort _maximumValue;
		private bool _isTemporary;

		public ushort CurrentValue => _currentValue;
		public ushort MaximumValue => _maximumValue;
		public bool IsTemporary => _isTemporary;

		public VcpGetResponseWaitState(byte vcpCode) => VcpCode = vcpCode;

		protected override void ProcessDdcResponseContents(ReadOnlySpan<byte> contents)
		{
			if (contents.Length != 7)
			{
				SetNewException(new InvalidDataException("The received response has an incorrect length."));
				return;
			}

			switch (contents[0])
			{
			case 0:
				if (contents[1] != VcpCode)
				{
					SetNewException(new InvalidDataException("The received response does not match the requested VCP code."));
				}

				_isTemporary = contents[2] != 0;
				_maximumValue = (ushort)(contents[3] << 8 | contents[4]);
				_currentValue = (ushort)(contents[5] << 8 | contents[6]);

				TaskCompletionSource.TrySetResult();
				return;
			case 1:
				SetNewException(new InvalidOperationException($"The monitor rejected the request for VCP code {VcpCode:X2} as unsupported. Some monitors can badly report VCP codes in the capabilities string."));
				return;
			default:
				SetNewException(new InvalidOperationException($"The monitor returned an unknown error for VCP code {VcpCode:X2}."));
				return;
			}
		}
	}

	private static byte DdcChecksum(ReadOnlySpan<byte> buffer, byte initialValue)
	{
		byte b = initialValue;
		for (int i = 0; i < buffer.Length; i++)
		{
			b ^= buffer[i];
		}
		return b;
	}

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private readonly byte _sessionId;
	private readonly byte _deviceAddress;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readAsyncTask;
	private readonly ConcurrentDictionary<byte, ResponseWaitState> _pendingOperations;
	private int _writeState;

	/// <summary>Creates a new instance of the class <see cref="HidI2CTransport"/>.</summary>
	/// <param name="stream">A stream to use for receiving and sending messages.</param>
	/// <param name="sessionId">A byte value to be used for identifying requests done by the instance.</param>
	/// <param name="cancellationToken"></param>
	public static Task<HidI2CTransport> CreateAsync(HidFullDuplexStream stream, byte sessionId, CancellationToken cancellationToken)
		=> CreateAsync(stream, sessionId, DefaultDdcDeviceAddress, cancellationToken);

	/// <summary>Creates a new instance of the class <see cref="HidI2CTransport"/>.</summary>
	/// <param name="stream">A stream to use for receiving and sending messages.</param>
	/// <param name="sessionId">A byte value to be used for identifying requests done by the instance.</param>
	/// <param name="deviceAddress">The address of the I2C device. Defaults to <c>0x37</c>.</param>
	/// <param name="cancellationToken"></param>
	public static async Task<HidI2CTransport> CreateAsync(HidFullDuplexStream stream, byte sessionId, byte deviceAddress, CancellationToken cancellationToken)
	{
		var transport = new HidI2CTransport(stream, sessionId, deviceAddress);
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

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _readAsyncTask.ConfigureAwait(false);
	}

	private HidI2CTransport(HidFullDuplexStream stream, byte sessionId, byte deviceAddress)
	{
		_stream = stream;
		_sessionId = sessionId;
		_deviceAddress = deviceAddress;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_buffers[65] = 0; // Zero-initialize the write report ID.
		_cancellationTokenSource = new CancellationTokenSource();
		_readAsyncTask = ReadAsync(_cancellationTokenSource.Token);
		_pendingOperations = new();
	}

	private byte BeginWrite()
	{
		int sequenceNumber = Volatile.Read(ref _writeState) & unchecked((int)0xFF000000U);
		while (true)
		{
			int oldState = Interlocked.CompareExchange(ref _writeState, sequenceNumber | WriteStateReserved, sequenceNumber | WriteStateReady);
			if (sequenceNumber != (sequenceNumber = oldState & unchecked((int)0xFF000000U)))
			{
				continue;
			}
			oldState &= 0xFFFFFF;
			if (oldState != WriteStateReady)
			{
				if (oldState == WriteStateDisposed) throw new ObjectDisposedException(nameof(HidI2CTransport));
				else throw new InvalidOperationException("A write operation is already pending.");
			}
			return (byte)(sequenceNumber >>> 24);
		}
	}

	private void EndWrite(byte oldSequenceNumber) => EndWrite(oldSequenceNumber, (byte)(oldSequenceNumber + 1));

	private void EndWrite(byte oldSequenceNumber, byte newSequenceNumber)
	{
		Interlocked.CompareExchange(ref _writeState, newSequenceNumber << 24 | WriteStateReady, oldSequenceNumber << 24 | WriteStateReserved);
	}

	private async Task HandshakeAsync(CancellationToken cancellationToken)
	{
		byte sequenceNumber = BeginWrite();
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

			WriteHandshakeRequest(buffer.Span[1..], _sessionId, sequenceNumber);

			var waitState = new HandshakeResponseWaitState();

			_pendingOperations.TryAdd(sequenceNumber, waitState);

			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				_pendingOperations.TryRemove(new(sequenceNumber, waitState));
				throw;
			}
		}
		finally
		{
			EndWrite(sequenceNumber);
		}
	}

	public async Task<(ushort CurrentValue, ushort MaximumValue, bool IsTemporary)> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		byte initialSequenceNumber = BeginWrite();
		byte sequenceNumber = initialSequenceNumber;
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteDdcVcpGetRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, vcpCode);
			sequenceNumber++;
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			// We need to wait 50ms after a VCP get request.
			var delay = Task.Delay(40, cancellationToken);
			WriteI2CReadRequestHeader(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x0b);
			byte currentOperationSequenceNumber = sequenceNumber++;
			var waitState = new VcpGetResponseWaitState(vcpCode);
			// Wait the delay
			await delay.ConfigureAwait(false);
			_pendingOperations.TryAdd(currentOperationSequenceNumber, waitState);
			try
			{
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

				return (waitState.CurrentValue, waitState.MaximumValue, waitState.IsTemporary);
			}
			catch (OperationCanceledException)
			{
				_pendingOperations.TryRemove(new(currentOperationSequenceNumber, waitState));
				throw;
			}
		}
		finally
		{
			EndWrite(initialSequenceNumber, sequenceNumber);
		}
	}

	public async Task SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
	{
		byte initialSequenceNumber = BeginWrite();
		byte sequenceNumber = initialSequenceNumber;
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
			WriteDdcVcpSetRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, vcpCode, value);
			sequenceNumber++;
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			// We need to wait 50ms after a VCP set request.
			var delay = Task.Delay(50, cancellationToken);
			// Wait the delay
			await delay.ConfigureAwait(false);
		}
		finally
		{
			EndWrite(initialSequenceNumber, sequenceNumber);
		}
	}

	public async Task<ushort> GetCapabilitiesAsync(Memory<byte> destination, CancellationToken cancellationToken)
	{
		byte initialSequenceNumber = BeginWrite();
		byte sequenceNumber = initialSequenceNumber;
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

			// NB: We should be able to avoid rewriting the whole buffer for every packet in table read requests, but for now, this will be simpler.
			WriteDdcVcpGetCapabilitiesRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, 0);

			var waitState = new DdcGetCapabilitiesWaitState(destination);

			while (true)
			{
				sequenceNumber++;
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				// We need to wait 50ms after a table read request.
				var delay = Task.Delay(50, cancellationToken);
				// Table requests can return up to 32 bytes at a time, and counting the 6 extra ddc packet wrapper that makes 38 bytes total. (0x26)
				WriteI2CReadRequestHeader(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x26);
				byte currentOperationSequenceNumber = sequenceNumber++;
				// Wait the delay
				await delay.ConfigureAwait(false);
				_pendingOperations.TryAdd(currentOperationSequenceNumber, waitState);
				try
				{
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
					await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
					if (waitState.IsCompleted)
					{
						return waitState.ReadLength;
					}
				}
				catch (OperationCanceledException)
				{
					_pendingOperations.TryRemove(new(currentOperationSequenceNumber, waitState));
					throw;
				}
				WriteDdcVcpGetCapabilitiesRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, waitState.ReadLength);
				waitState.Reset();
			}
		}
		finally
		{
			EndWrite(initialSequenceNumber, sequenceNumber);
		}
	}

	public async Task<ushort> ReadTableAsync(byte vcpCode, Memory<byte> destination, CancellationToken cancellationToken)
	{
		byte initialSequenceNumber = BeginWrite();
		byte sequenceNumber = initialSequenceNumber;
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

			// NB: We should be able to avoid rewriting the whole buffer for every packet in table read requests, but for now, this will be simpler.
			WriteDdcVcpReadTableRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, vcpCode, 0);

			var waitState = new VcpReadTableWaitState(destination);

			while (true)
			{
				sequenceNumber++;
				await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				// We need to wait 50ms after a table read request.
				var delay = Task.Delay(50, cancellationToken);
				// Table requests can return up to 32 bytes at a time, and counting the 6 extra ddc packet wrapper that makes 38 bytes total. (0x26)
				WriteI2CReadRequestHeader(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x26);
				byte currentOperationSequenceNumber = sequenceNumber++;
				// Wait the delay
				await delay.ConfigureAwait(false);
				_pendingOperations.TryAdd(currentOperationSequenceNumber, waitState);
				try
				{
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
					await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
					if (waitState.IsCompleted)
					{
						return waitState.ReadLength;
					}
				}
				catch (OperationCanceledException)
				{
					_pendingOperations.TryRemove(new(currentOperationSequenceNumber, waitState));
					throw;
				}
				WriteDdcVcpReadTableRequest(buffer.Span[1..], _sessionId, sequenceNumber, _deviceAddress, 0x51, vcpCode, waitState.ReadLength);
				waitState.Reset();
			}
		}
		finally
		{
			EndWrite(initialSequenceNumber, sequenceNumber);
		}
	}

	private static void WriteHandshakeRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber)
	{
		buffer.Clear();

		buffer[0] = 0x0C;
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

	private static void WriteDdcVcpGetRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte rawSourceAddress, byte vcpCode)
	{
		buffer.Clear();

		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, 5);

		buffer = buffer.Slice(8, 5);

		// The DDC source device should be device 0x28 (host), which maps to the values 0x50/0x51. (device_address << 1 | direction_bit)
		// When sending data to the display, the address 0x51 should be used, while address 0x50 will be used when receiving data.
		// However, LG monitors are using a hack to expose custom features by writing to the address 0x51 instead of 0x51.
		// In all cases, the source register could also be used to access display-dependent devices, as explained in the DDC documentation.
		// This use-case of having display-dependent devices is highly unlikely in the modern world, though.
		buffer[0] = rawSourceAddress;
		buffer[1] = 0x82;
		buffer[2] = (byte)DdcCiCommand.VcpRequest;
		buffer[3] = vcpCode;
		buffer[4] = DdcChecksum(buffer[..4], 0x6E);
	}

	private static void WriteDdcVcpGetCapabilitiesRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte rawSourceAddress, ushort offset)
	{
		buffer.Clear();

		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, 6);

		buffer = buffer.Slice(8, 6);

		buffer[0] = rawSourceAddress;
		buffer[1] = 0x83;
		buffer[2] = (byte)DdcCiCommand.CapabilitiesRequest;
		buffer[3] = (byte)(offset >> 8);
		buffer[4] = (byte)offset;
		buffer[5] = DdcChecksum(buffer[..5], 0x6E);
	}

	private static void WriteDdcVcpReadTableRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte rawSourceAddress, byte vcpCode, ushort offset)
	{
		buffer.Clear();

		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, 7);

		buffer = buffer.Slice(8, 7);

		buffer[0] = rawSourceAddress;
		buffer[1] = 0x84;
		buffer[2] = (byte)DdcCiCommand.TableReadRequest;
		buffer[3] = vcpCode;
		buffer[4] = (byte)(offset >> 8);
		buffer[5] = (byte)offset;
		buffer[6] = DdcChecksum(buffer[..6], 0x6E);
	}

	private static void WriteDdcVcpSetRequest(Span<byte> buffer, byte sessionId, byte sequenceNumber, byte deviceAddress, byte rawSourceAddress, byte vcpCode, ushort value)
	{
		buffer.Clear();

		WriteI2CWriteRequestHeader(buffer, sessionId, sequenceNumber, deviceAddress, 7);

		buffer = buffer.Slice(8, 7);

		// The DDC source device should be device 0x28 (host), which maps to the values 0x50/0x51. (device_address << 1 | direction_bit)
		// When sending data to the display, the address 0x51 should be used, while address 0x50 will be used when receiving data.
		// However, LG monitors are using a hack to expose custom features by writing to the address 0x51 instead of 0x51.
		// In all cases, the source register could also be used to access display-dependent devices, as explained in the DDC documentation.
		// This use-case of having display-dependent devices is highly unlikely in the modern world, though.
		buffer[0] = rawSourceAddress;
		buffer[1] = 0x84;
		buffer[2] = (byte)DdcCiCommand.VcpSet;
		buffer[3] = vcpCode;
		buffer[4] = (byte)(value >>> 8);
		buffer[5] = (byte)value;
		buffer[6] = DdcChecksum(buffer[..6], 0x6E);
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, 65);
		try
		{
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
		catch
		{
			// TODO: Log the exception
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		if (message[2] != _sessionId) return;

		byte sequenceNumber = message[1];

		_pendingOperations.TryRemove(sequenceNumber, out var state);

		// The first byte in the raw HID response messages seem to always be the message data length. From what we can infer from the data, it needs to be at least 4 bytes.
		// TODO: Emit a log when there is an error and no state to bubble up the error.
		if (message[0] < 4)
		{
			state?.SetNewException(new InvalidDataException("The received response has an invalid length."));
			return;
		}
		else if (message[3] != 0x00)
		{
			state?.SetNewException(new InvalidDataException("The received response contains an unexpected byte value."));
			return;
		}

		state?.OnDataReceived(message[4..message[0]]);
	}
}
