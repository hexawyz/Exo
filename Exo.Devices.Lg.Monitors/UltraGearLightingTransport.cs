using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;
using Exo.ColorFormats;
using Exo.Service;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Lg.Monitors;

internal sealed class UltraGearLightingTransport : IAsyncDisposable
{
	private enum Command : byte
	{
		SetVideoSyncColors = 0xC1,
		SetAudioSyncColors = 0xC2,
		GetLedInformation = 0xC5,
		SetActiveEffect = 0xC7,
		GetActiveEffect = 0xC8,
		EnableLightingEffect = 0xCA,
		GetLightingStatus = 0xCE,
		SetLightingStatus = 0xCF,
	}

	private enum Direction : byte
	{
		Get = 1,
		Set = 2,
	}

	private class CommandResponseWaitState : ResponseWaitState
	{
		public Command Command { get; }
		public Direction Direction { get; }

		public CommandResponseWaitState(Command command, Direction direction)
		{
			Command = command;
			Direction = direction;
		}

		public override void OnDataReceived(ReadOnlySpan<byte> message) => TaskCompletionSource.TrySetResult();
	}

	private class LedInformationCommandResponseWaitState : CommandResponseWaitState
	{
		public byte LedCount { get; private set; }

		public LedInformationCommandResponseWaitState() : base(Command.GetLedInformation, Direction.Get) { }

		public override void OnDataReceived(ReadOnlySpan<byte> message)
		{
			LedCount = message[0];
			TaskCompletionSource.TrySetResult();
		}
	}

	private class ActiveEffectResponseWaitState : CommandResponseWaitState
	{
		public LightingEffect Effect { get; private set; }

		public ActiveEffectResponseWaitState() : base(Command.GetActiveEffect, Direction.Get) { }

		public override void OnDataReceived(ReadOnlySpan<byte> message)
		{
			Effect = (LightingEffect)message[5];
			TaskCompletionSource.TrySetResult();
		}
	}

	private class LightingStatusResponseWaitState : CommandResponseWaitState
	{
		public bool IsLightingEnabled { get; private set; }
		public byte MinimumBrightnessLevel { get; private set; }
		public byte MaximumBrightnessLevel { get; private set; }
		public byte CurrentBrightnessLevel { get; private set; }

		public LightingStatusResponseWaitState() : base(Command.GetLightingStatus, Direction.Get) { }

		public override void OnDataReceived(ReadOnlySpan<byte> message)
		{
			// 1 = ON; 2 = OFF;
			IsLightingEnabled = message[1] == 1;
			MinimumBrightnessLevel = message[3];
			MaximumBrightnessLevel = message[4];
			CurrentBrightnessLevel = message[5];
			TaskCompletionSource.TrySetResult();
		}
	}

	private static readonly object DisposedSentinel = new object();

	// The message length is hardcoded to 64 bytes.
	// This cannot be changed without a major refactoring of the rest of the code.
	private const int HidMessageLength = 64;

	// The message length is hardcoded to 64 bytes + report ID.
	private const int HidBufferLength = HidMessageLength + 1;

	// The write buffer length will hold up to two messages plus a report ID byte.
	private const int WriteBufferLength = HidBufferLength + HidMessageLength;

	private readonly HidFullDuplexStream _stream;

	private readonly byte[] _buffers;
	// We need to track the last written length at two different places because some operations will use only one buffer while others will use two.
	// Single-buffer operations will not clear the second buffer, but further multi-buffer operation must still be aware of the facts.
	private ushort _lastWrittenLengthSingleBuffer;
	private ushort _lastWrittenLengthTwoBuffers;

	private object? _currentWaitState;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;

	private readonly ILogger<UltraGearLightingTransport> _logger;

	public UltraGearLightingTransport(HidFullDuplexStream stream, ILogger<UltraGearLightingTransport> logger)
	{
		_stream = stream;
		_logger = logger;
		// Allocate 1 read buffer + 1 write buffer with extra capacity for one message.
		_buffers = GC.AllocateUninitializedArray<byte>(HidBufferLength + WriteBufferLength, true);
		ResetWriteBuffer(_buffers.AsSpan(HidBufferLength), WriteBufferLength);
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		Volatile.Write(ref _currentWaitState, DisposedSentinel);
		await _readTask.ConfigureAwait(false);
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_buffers, HidBufferLength, HidBufferLength);
	private Memory<byte> WriteBuffers => MemoryMarshal.CreateFromPinnedArray(_buffers, HidBufferLength, WriteBufferLength);

	// We'll always keep the write buffer ready for further operations.
	// ATM there are no regular write operations taking less than 11 bytes total, and those will always start with the same prefix, so we can avoid clearing the buffer in these cases.
	//private void ResetWriteBuffer(int messageLength) => ResetWriteBuffer(WriteBuffer.Span, messageLength);

	private static void ResetWriteBuffer(Span<byte> buffer, int messageLength)
	{
		buffer[..messageLength].Clear();
		// Initialize the write report with the standard write header
		buffer[1] = 0x53;
		buffer[2] = 0x43;
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

				remaining = remaining[count..];
			}
			while (remaining.Length != 0);

			ProcessReadMessage(buffer.Span[1..]);
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> span)
	{
		if (span[0] != 0x52)
		{
			_logger.UltraGearLightingTransportInvalidMessageHeader(span[0]);
			return;
		}

		byte length = span[3];

		if (Checksum.Xor(span[..(length + 5)], 0) != 0)
		{
			_logger.UltraGearLightingTransportInvalidMessageChecksum(span[length + 5], Checksum.Xor(span[..(length + 4)], 0));
			return;
		}

		var state = Volatile.Read(ref _currentWaitState);

		if (state is null || ReferenceEquals(state, DisposedSentinel)) return;

		var waitState = Unsafe.As<CommandResponseWaitState>(state);

		if (span[1] == (byte)waitState.Command && span[2] == (byte)waitState.Direction)
		{
			try
			{
				waitState.OnDataReceived(span.Slice(4, length));
			}
			catch (Exception ex)
			{
				_logger.UltraGearLightingTransportMessageProcessingError(ex);
			}
		}
	}

	private void BeginWrite(ResponseWaitState waitState)
	{
		var previousState = Interlocked.CompareExchange(ref _currentWaitState, waitState, null);
		if (previousState is not null)
		{
			ObjectDisposedException.ThrowIf(ReferenceEquals(previousState, DisposedSentinel), typeof(UltraGearLightingTransport));
			throw new InvalidOperationException("Another command is already pending.");
		}
	}

	private void EndWrite(ResponseWaitState waitState) => Interlocked.CompareExchange(ref _currentWaitState, null, waitState);

	public async Task<byte> GetLedCountAsync(CancellationToken cancellationToken)
	{
		var ws = new LedInformationCommandResponseWaitState();
		await ExecuteSimpleCommandAsync(ws, 0, 0, cancellationToken).ConfigureAwait(false);
		return ws.LedCount;
	}

	public Task SetActiveEffectAsync(LightingEffect effect, CancellationToken cancellationToken)
		=> ExecuteSimpleCommandAsync(Command.SetActiveEffect, Direction.Set, 0, (byte)effect, cancellationToken);

	public async Task<LightingEffect> GetActiveEffectAsync(CancellationToken cancellationToken)
	{
		var ws = new ActiveEffectResponseWaitState();
		await ExecuteSimpleCommandAsync(ws, 0, 0, cancellationToken).ConfigureAwait(false);
		return ws.Effect;
	}

	public async Task<LightingInformation> GetLightingStatusAsync(CancellationToken cancellationToken)
	{
		var ws = new LightingStatusResponseWaitState();
		await ExecuteSimpleCommandAsync(ws, 0, 0, cancellationToken).ConfigureAwait(false);
		return new(ws.IsLightingEnabled, ws.MinimumBrightnessLevel, ws.MaximumBrightnessLevel, ws.CurrentBrightnessLevel);
	}

	public Task SetLightingStatusAsync(bool enable, byte brightnessLevel, CancellationToken cancellationToken)
		=> ExecuteSimpleCommandAsync(Command.SetLightingStatus, Direction.Set, enable ? (byte)1 : (byte)2, brightnessLevel, cancellationToken);

	public Task EnableLightingEffectAsync(LightingEffect effect, CancellationToken cancellationToken)
		=> ExecuteSimpleCommandAsync(Command.EnableLightingEffect, Direction.Set, 3, (byte)effect, cancellationToken);

	public Task SetAudioSyncColors(ReadOnlySpan<RgbColor> ledColors, CancellationToken cancellationToken)
		=> SetDynamicColors(Command.SetAudioSyncColors, ledColors, cancellationToken);

	public Task SetAudioSyncColors(RgbColor color, byte count, CancellationToken cancellationToken)
		=> SetDynamicColors(Command.SetAudioSyncColors, color, count, cancellationToken);

	public Task SetVideoSyncColors(ReadOnlySpan<RgbColor> ledColors, CancellationToken cancellationToken)
		=> SetDynamicColors(Command.SetVideoSyncColors, ledColors, cancellationToken);

	public Task SetVideoSyncColors(RgbColor color, byte count, CancellationToken cancellationToken)
		=> SetDynamicColors(Command.SetVideoSyncColors, color, count, cancellationToken);

	private Task SetDynamicColors(Command command, RgbColor color, byte colorCount, CancellationToken cancellationToken)
	{
		var waitState = new CommandResponseWaitState(command, Direction.Set);
		BeginWrite(waitState);
		var buffer = WriteBuffers;
		int writtenLength = WriteColorBuffers(buffer.Span, command, color, colorCount);
		// Clear the extra data from previous writes.
		// This may clear the second buffer unnecessarily, but it should still be easier to manage it that way.
		if (_lastWrittenLengthTwoBuffers > writtenLength)
		{
			buffer.Span[writtenLength.._lastWrittenLengthTwoBuffers].Clear();
		}
		_lastWrittenLengthTwoBuffers = _lastWrittenLengthSingleBuffer = (ushort)writtenLength;
		return ExecuteMultipartRequestAsync(waitState, writtenLength > HidBufferLength ? buffer : buffer[..HidBufferLength], cancellationToken);
	}

	private Task SetDynamicColors(Command command, ReadOnlySpan<RgbColor> ledColors, CancellationToken cancellationToken)
	{
		var waitState = new CommandResponseWaitState(command, Direction.Set);
		BeginWrite(waitState);
		var buffer = WriteBuffers;
		int writtenLength = WriteColorBuffers(buffer.Span, command, MemoryMarshal.AsBytes(ledColors));
		// Clear the extra data from previous writes.
		// This may clear the second buffer unnecessarily, but it should still be easier to manage it that way.
		if (_lastWrittenLengthTwoBuffers > writtenLength)
		{
			buffer.Span[writtenLength.._lastWrittenLengthTwoBuffers].Clear();
		}
		_lastWrittenLengthTwoBuffers = _lastWrittenLengthSingleBuffer = (ushort)writtenLength;
		return ExecuteMultipartRequestAsync(waitState, writtenLength > HidBufferLength ? buffer : buffer[..HidBufferLength], cancellationToken);
	}

	private static int WriteColorBuffers(Span<byte> buffer, Command command, RgbColor color, byte colorCount)
	{
		// Using the two static buffers that we have, we can write up to 39 colors.
		if (colorCount > 39) throw new InvalidOperationException("Too many colors specified.");

		int writeLength = 1 + 3 * colorCount;
		WriteRequestHeader(buffer[1..], command, Direction.Set, (byte)writeLength);
		writeLength += 6;
		buffer[6] = 0; // No idea what this byte should be but it does seem to be an offset.
		MemoryMarshal.Cast<byte, RgbColor>(buffer.Slice(7, colorCount * 3)).Fill(color);
		WriteRequestTrailer(buffer[1..], writeLength - 1);

		writeLength += 3;

		return writeLength;
	}

	private static int WriteColorBuffers(Span<byte> buffer, Command command, ReadOnlySpan<byte> data)
	{
		// Using the two static buffers that we have, we can write exactly 118 data bytes and nothing more
		if (data.IsEmpty || data.Length > 118) throw new InvalidOperationException("Too many colors specified.");

		int writeLength = 1 + data.Length;
		WriteRequestHeader(buffer[1..], command, Direction.Set, (byte)writeLength);
		writeLength += 6;
		buffer[6] = 0; // No idea what this byte should be but it does seem to be an offset.
		data.CopyTo(buffer[7..]);
		WriteRequestTrailer(buffer[1..], writeLength - 1);

		writeLength += 3;

		return writeLength;
	}

	private Task ExecuteSimpleCommandAsync(Command command, Direction direction, byte parameter1, byte parameter2, CancellationToken cancellationToken)
		=> ExecuteSimpleCommandAsync(new(command, direction), parameter1, parameter2, cancellationToken);

	private Task ExecuteSimpleCommandAsync(CommandResponseWaitState waitState, byte parameter1, byte parameter2, CancellationToken cancellationToken)
	{
		BeginWrite(waitState);

		var buffer = WriteBuffer;
		var span = WriteBuffer.Span;

		WriteSimpleRequest(span[1..], waitState.Command, waitState.Direction, parameter1, parameter2);

		// Clear the extra buffer contents from the last write.
		// The condition will be true sometimes, depending on the usage, but it should generally be false.
		if (_lastWrittenLengthSingleBuffer > 11)
		{
			span[11..Math.Min(HidBufferLength, (int)_lastWrittenLengthSingleBuffer)].Clear();
		}
		_lastWrittenLengthSingleBuffer = 11;

		return ExecuteRequestAsync(waitState, buffer, cancellationToken);
	}

	private async Task ExecuteRequestAsync(CommandResponseWaitState waitState, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		try
		{
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndWrite(waitState);
		}
	}

	// NB: This is a destructive operation for the buffer.
	// The last byte of each block will be erased, except for the last one.
	private async Task ExecuteMultipartRequestAsync(CommandResponseWaitState waitState, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		var remaining = buffer;
		try
		{
			while (true)
			{
				await _stream.WriteAsync(remaining[..HidBufferLength], cancellationToken).ConfigureAwait(false);
				remaining = remaining.Slice(HidBufferLength - 1);
				if (remaining.Length == 1) break;
				remaining.Span[0] = 0;
			}
			await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndWrite(waitState);
		}
	}

	private static void WriteRequestHeader(Span<byte> buffer, Command command, Direction direction, byte dataLength)
	{
		buffer[2] = (byte)command;
		buffer[3] = (byte)direction;
		buffer[4] = dataLength;
	}

	private static void WriteSimpleRequest(Span<byte> buffer, Command command, Direction direction, byte parameter1, byte parameter2)
	{
		WriteRequestHeader(buffer, command, direction, 2);
		buffer[5] = parameter1;
		buffer[6] = parameter2;
		WriteRequestTrailer(buffer, 7);
	}

	private static void WriteRequestTrailer(Span<byte> buffer, int writtenLength)
	{
		byte checksum = Checksum.Xor(buffer[..writtenLength], 0);
		buffer = buffer[writtenLength..];

		buffer[0] = checksum;
		buffer[1] = 0x45;
		buffer[2] = 0x44;
	}
}
