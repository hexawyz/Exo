using System.Runtime.InteropServices;
using DeviceTools.DisplayDevices;

namespace Exo.I2C;

// TODO: We can make the delays deferred rather than immediate. (Remember the time of the last operation then wait at the beginning of the next one if necessary)
public class DisplayDataChannel : IDisposable
{
	// We need to wait 40ms after a VCP Request operation.
	public const int VcpRequestDelay = 40;
	// We need to wait 40ms after a VCP Set operation.
	public const int VcpSetDelay = 50;
	// We need to wait 40ms after a Capabilities Reply operation.
	public const int CapabilitiesReplyDelay = 50;

	private readonly byte[] _buffer;
	private II2CBus? _i2cBus;
	private readonly bool _isOwned;

	protected II2CBus I2CBus
	{
		get
		{
			var i2cBus = _i2cBus;
			ObjectDisposedException.ThrowIf(i2cBus is null, GetType());
			return i2cBus;
		}
	}

	protected Memory<byte> Buffer => MemoryMarshal.CreateFromPinnedArray(_buffer, 0, _buffer.Length);

	public DisplayDataChannel(II2CBus? i2cBus, bool isOwned)
		: this(i2cBus, 40, isOwned)
	{
		// We should generally not need anything more than 37 bytes for the buffer, so this main constructor requests a 40 byte buffer.
		// 40 bytes should fit neatly in a 64 byte sequence, considering the object header and array length.
	}

	protected DisplayDataChannel(II2CBus? i2cBus, byte bufferLength, bool isOwned)
	{
		_buffer = GC.AllocateUninitializedArray<byte>(bufferLength, pinned: true);
		_i2cBus = i2cBus;
		_isOwned = isOwned;
	}

	public void Dispose()
	{
		if (_isOwned && Interlocked.Exchange(ref _i2cBus, null) is { } i2cBus)
		{
			i2cBus.Dispose();
		}
	}

	protected static void WriteVcpRequest(Span<byte> buffer, byte vcpCode, byte checksumInitialValue)
	{
		buffer[0] = 0x82;
		buffer[1] = (byte)DdcCiCommand.VcpRequest;
		buffer[2] = vcpCode;
		buffer[3] = (byte)(0x83 ^ checksumInitialValue ^ vcpCode);
	}

	protected static void WriteVcpSet(Span<byte> buffer, byte vcpCode, ushort value, byte checksumInitialValue)
	{
		buffer[0] = 0x84;
		buffer[1] = (byte)DdcCiCommand.VcpSet;
		buffer[2] = vcpCode;
		BigEndian.Write(ref buffer[3], value);
		buffer[5] = Checksum.Xor(buffer[..5], checksumInitialValue);
	}

	protected static void WriteVariableRead(Span<byte> buffer, DdcCiCommand command, ushort offset, byte checksumInitialValue)
	{
		buffer[0] = 0x83;
		buffer[1] = (byte)command;
		BigEndian.Write(ref buffer[2], offset);
		buffer[4] = Checksum.Xor(buffer[..4], checksumInitialValue);
	}

	protected static void WriteCapabilitiesRequest(Span<byte> buffer, ushort offset, byte checksumInitialValue)
		=> WriteVariableRead(buffer, DdcCiCommand.CapabilitiesRequest, offset, checksumInitialValue);

	protected static void WriteTableReadRequest(Span<byte> buffer, ushort offset, byte checksumInitialValue)
		=> WriteVariableRead(buffer, DdcCiCommand.TableReadRequest, offset, checksumInitialValue);

	private static ReadOnlySpan<byte> ValidateDdcResponse(ReadOnlySpan<byte> message, DdcCiCommand command)
	{
		if (message[0] != 0x6E)
		{
			throw new InvalidDataException("The received response has an unexpected destination address.");
		}

		if (message[1] < 0x81)
		{
			throw new InvalidDataException("The received response has an invalid length.");
		}

		byte length = (byte)(message[1] & 0x7F);

		if (Checksum.Xor(message[..(length + 3)], 0x50) != 0)
		{
			throw new InvalidDataException("The received response has an invalid DDC checksum.");
		}

		if (message[2] != (byte)command)
		{
			throw new InvalidDataException("The received response is referencing the wrong DDC opcode.");
		}

		return message.Slice(3, length - 1);
	}

	private static VcpFeatureResponse ReadVcpFeatureReply(ReadOnlySpan<byte> message, byte vcpCode)
	{
		var contents = ValidateDdcResponse(message, DdcCiCommand.VcpReply);

		if (contents.Length != 7)
		{
			throw new InvalidDataException("The received response has an incorrect length.");
		}

		switch (contents[0])
		{
		case 0:
			if (contents[1] != vcpCode)
			{
				throw new InvalidDataException("The received response does not match the requested VCP code.");
			}

			bool isMomentary = contents[2] != 0;
			ushort maximumValue = BigEndian.ReadUInt16(contents[3]);
			ushort currentValue = BigEndian.ReadUInt16(contents[5]);

			return new(currentValue, maximumValue, isMomentary);
		case 1:
			throw new InvalidOperationException($"The monitor rejected the request for VCP code {vcpCode:X2} as unsupported. Some monitors can badly report VCP codes in the capabilities string.");
		default:
			throw new InvalidOperationException($"The monitor returned an unknown error for VCP code {vcpCode:X2}.");
		}
	}

	private static bool ReadVariableLengthResponse(Span<byte> destination, ReadOnlySpan<byte> message, DdcCiCommand command, ref ushort offset)
	{
		var contents = ValidateDdcResponse(message, command);

		if (BigEndian.ReadUInt16(contents[0]) != offset)
		{
			throw new InvalidDataException("Non consecutive data packets were received.");
		}

		var data = contents[2..];

		if (data.Length == 0)
		{
			return true;
		}

		int nextOffset = offset + data.Length;

		if (nextOffset > 0xFFFF)
		{
			throw new InvalidDataException("The data exceeded the maximum size.");
		}

		if (destination.Length < data.Length)
		{
			data[..destination.Length].CopyTo(destination);
			throw new InvalidOperationException("The provided buffer is too small.");
		}
		else
		{
			data.CopyTo(destination);
		}

		offset = (ushort)nextOffset;
		return false;
	}

	public async ValueTask<VcpFeatureResponse> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		var i2cBus = I2CBus;
		var buffer = Buffer;
		WriteVcpRequest(buffer.Span, vcpCode, 0x6E ^ 0x51);
		await i2cBus.WriteAsync(0x6E, 0x51, buffer[..4], cancellationToken).ConfigureAwait(false);
		await Task.Delay(VcpRequestDelay, cancellationToken).ConfigureAwait(false);
		await i2cBus.ReadAsync(0x6F, buffer[..11], cancellationToken).ConfigureAwait(false);
		return ReadVcpFeatureReply(buffer.Span[..11], vcpCode);
	}

	public async ValueTask SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
	{
		var i2cBus = I2CBus;
		var buffer = Buffer;
		WriteVcpSet(buffer.Span, vcpCode, value, 0x6E ^ 0x51);
		await i2cBus.WriteAsync(0x6E, 0x51, buffer[..6], cancellationToken).ConfigureAwait(false);
		await Task.Delay(VcpSetDelay, cancellationToken).ConfigureAwait(false);
	}

	protected async ValueTask<ushort> GetVariableLengthAsync(Memory<byte> destination, DdcCiCommand requestCommand, DdcCiCommand replyCommand, int delay, CancellationToken cancellationToken)
	{
		var i2cBus = I2CBus;
		var buffer = Buffer;

		ushort offset = 0;
		while (true)
		{
			WriteVariableRead(buffer.Span, requestCommand, offset, 0x6E ^ 0x51);
			await i2cBus.WriteAsync(0x6E, 0x51, buffer[..5], cancellationToken).ConfigureAwait(false);
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			await i2cBus.ReadAsync(0x6F, buffer[..38], cancellationToken).ConfigureAwait(false);
			if (ReadVariableLengthResponse(destination.Span[offset..], buffer.Span[..38], replyCommand, ref offset)) break;
		}
		return offset;
	}

	public ValueTask<ushort> GetCapabilitiesAsync(Memory<byte> destination, CancellationToken cancellationToken)
		=> GetVariableLengthAsync(destination, DdcCiCommand.CapabilitiesRequest, DdcCiCommand.CapabilitiesReply, CapabilitiesReplyDelay, cancellationToken);
}

public record struct VcpFeatureResponse
{
	public ushort CurrentValue { get; }
	public ushort MaximumValue { get; }
	public bool IsMomentary { get; }

	public VcpFeatureResponse(ushort currentValue, ushort maximumValue, bool isMomentary)
	{
		CurrentValue = currentValue;
		MaximumValue = maximumValue;
		IsMomentary = isMomentary;
	}
}
