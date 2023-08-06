using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Lg.Monitors;

internal sealed class UltraGearLightingTransport
{
	private enum Command : byte
	{
		SetActiveEffect = 0xC7,
		EnableLightingEffect = 0xCA,
		EnableLighting = 0xCF,
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

	private static readonly object DisposedSentinel = new object();

	// The message length is hardcoded to 64 bytes + report ID.
	private const int MessageLength = 65;

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private object? _currentWaitState;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;

	public UltraGearLightingTransport(HidFullDuplexStream stream)
	{
		_stream = stream;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		ResetWriteBuffer(MessageLength);
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	private Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

	// We'll always keep the write buffer ready for further operations.
	// ATM there are no regular write operations taking less than 11 bytes total, and those will always start with the same prefix, so we can avoid clearing the buffer in these cases.
	private void ResetWriteBuffer(int messageLength)
	{
		var buffer = WriteBuffer.Span;

		buffer[..messageLength].Clear();
		// Initialize the write report with the standard write header
		buffer[1] = 0x53;
		buffer[2] = 0x43;
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
			// TODO: Log
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> span)
	{
		// TODO: Log an error and/or do something more specific.
		if (span[0] != 0x52) return;

		byte length = span[3];

		// TODO: Log an error and/or do something more specific.
		if (Checksum.Xor(span[..(length + 5)], 0) != 0) return;

		var state = Volatile.Read(ref _currentWaitState);

		if (state is null || ReferenceEquals(state, DisposedSentinel)) return;

		var waitState = Unsafe.As<CommandResponseWaitState>(state);

		if (span[1] == (byte)waitState.Command && span[2] == (byte)waitState.Direction)
		{
			waitState.OnDataReceived(span.Slice(4, length));
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

	public async Task SetActiveEffectAsync(LightingEffect effect, CancellationToken cancellationToken)
	{
		// TODO: The request generation code could be migrated into the command state. See if it's worth it. (Might not be interesting for long commands)
		var waitState = new CommandResponseWaitState(Command.SetActiveEffect, Direction.Set);
		BeginWrite(waitState);
		var buffer = WriteBuffer;
		try
		{
			WriteSimpleRequest(buffer.Span[1..], waitState.Command, waitState.Direction, 0, (byte)effect);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndWrite(waitState);
		}
	}

	public async Task EnableLightingAsync(bool enable, CancellationToken cancellationToken)
	{
		// TODO: The request generation code could be migrated into the command state. See if it's worth it. (Might not be interesting for long commands)
		var waitState = new CommandResponseWaitState(Command.EnableLighting, Direction.Set);
		BeginWrite(waitState);
		var buffer = WriteBuffer;
		try
		{
			WriteSimpleRequest(buffer.Span[1..], waitState.Command, waitState.Direction, enable ? (byte)1 : (byte)2, 0);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndWrite(waitState);
		}
	}

	public async Task EnableLightingEffectAsync(LightingEffect effect, CancellationToken cancellationToken)
	{
		// TODO: The request generation code could be migrated into the command state. See if it's worth it. (Might not be interesting for long commands)
		var waitState = new CommandResponseWaitState(Command.EnableLightingEffect, Direction.Set);
		BeginWrite(waitState);
		var buffer = WriteBuffer;
		try
		{
			WriteSimpleRequest(buffer.Span[1..], waitState.Command, waitState.Direction, 3, (byte)effect);
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			await waitState.TaskCompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndWrite(waitState);
		}
	}

	private static void WriteSimpleRequest(Span<byte> buffer, Command command, Direction direction, byte parameter1, byte parameter2)
	{
		buffer[2] = (byte)command;
		buffer[3] = (byte)direction;
		buffer[4] = 2; // Number of parameters
		buffer[5] = parameter1;
		buffer[6] = parameter2;
		CompleteRequest(buffer, 7);
	}

	private static void CompleteRequest(Span<byte> buffer, byte writtenLength)
	{
		byte checksum = Checksum.Xor(buffer[..writtenLength], 0);
		buffer = buffer[writtenLength..];

		buffer[0] = checksum;
		buffer[1] = 0x45;
		buffer[2] = 0x44;
	}
}
