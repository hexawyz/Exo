using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Elgato.StreamDeck;

// This implementation is for Stream Deck XL. Multiple subclasses are needed to provide features for other device versions.
// TODO: Create(ushort productId) returning the correct underlying implementation with correctly initialized parameters.
internal class StreamDeckDevice : IAsyncDisposable
{
	// https://www.reddit.com/r/elgato/comments/jagj6p/stream_deck_update_49_screensaver_sleep_action/
	// > The recommended screensaver dimensions are:
	// > • Stream Deck Mini: 320x240
	// > • Stream Deck: 480x272
	// > • Stream Deck XL: 1024x600

	private static readonly Dictionary<ushort, StreamDeckDeviceInfo> DeviceInformations = new()
	{
		{ 0x0060, new(3, 5, 72, 72, 480, 272) },
		{ 0x0063, new(2, 3, 80, 80, 320, 240) },
		{ 0x006C, new(4, 8, 96, 96, 1024, 600) },
		{ 0x006D, new(2, 3, 72, 72, 320, 240) },
	};

	private const int WriteBufferLength = 1024;
	private const int ReadBufferLength = 512;
	private const int FeatureBufferLength = 32;

	private readonly HidFullDuplexStream _stream;
	private readonly StreamDeckDeviceInfo _deviceInfo;
	private readonly byte[] _ioBuffers;
	private uint _downKeys;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _readTask;

	public StreamDeckDevice(HidFullDuplexStream stream, ushort productId)
	{
		_stream = stream;
		_deviceInfo = DeviceInformations[productId];
		_ioBuffers = GC.AllocateUninitializedArray<byte>(WriteBufferLength + ReadBufferLength + FeatureBufferLength, pinned: true);
		_cancellationTokenSource = new();
		_readTask = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _readTask.ConfigureAwait(false);
	}

	private Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, 0, WriteBufferLength);
	private Memory<byte> ReadBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, WriteBufferLength, ReadBufferLength);
	private ReadOnlySpan<byte> ReadBufferSpan => _ioBuffers.AsSpan(WriteBufferLength, ReadBufferLength);
	private Memory<byte> FeatureBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, WriteBufferLength + ReadBufferLength, FeatureBufferLength);

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				int count = await _stream.ReadAsync(ReadBuffer, cancellationToken).ConfigureAwait(false);
				if (count == 0) return;

				uint oldKeys = _downKeys;
				uint newKeys = ReadKeysFromBuffer();
				Volatile.Write(ref _downKeys, newKeys);
				uint changedKeys = oldKeys ^ newKeys;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
		}
	}

	private uint ReadKeysFromBuffer()
	{
		var buffer = ReadBufferSpan;

		// The bytes read from the device are:
		// [0]: Report ID
		// [1]: ?
		// [2]: Key Count
		// [3]: ?
		// [4..]: bool
		if (buffer[0] != 1) throw new InvalidOperationException("Invalid report ID.");

		var keyBuffer = buffer[4..buffer[2]];
		if (keyBuffer.Length > _deviceInfo.ButtonCount) throw new InvalidOperationException("Key count mismatch.");

		uint keys = 0;
		for (int i = 0; i < keyBuffer.Length; i++)
		{
			if (keyBuffer[i] != 0) keys |= 1u << i;
		}

		return keys;
	}

	// This returns device information similar to the one we hardcode here, so this method might not be ultra useful for now.
	// Only the fields whose meaning was obvious are mapped. The device also returns some other values whose meaning is yet unknown.
	public async Task<StreamDeckDeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken)
	{
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x08;

		static StreamDeckDeviceInfo ReadResponse(ReadOnlySpan<byte> buffer)
		{
			byte rowCount = buffer[1];
			byte columnCount = buffer[2];

			ushort imageWidth = LittleEndian.ReadUInt16(in buffer[3]);
			ushort imageHeight = LittleEndian.ReadUInt16(in buffer[5]);

			ushort screensaverWidth = LittleEndian.ReadUInt16(in buffer[7]);
			ushort screensaverHeight = LittleEndian.ReadUInt16(in buffer[9]);

			return new StreamDeckDeviceInfo(rowCount, columnCount, imageWidth, imageHeight, screensaverWidth, screensaverHeight);
		}

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<string> GetSerialNumberAsync(CancellationToken cancellationToken)
	{
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x06;
		static string ReadResponse(ReadOnlySpan<byte> buffer) => Encoding.ASCII.GetString(buffer.Slice(2, buffer[1]));

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
	{
		// TODO: There are unknown bytes here.
		// Values of all unknown bytes changed after a fw update
		// 05 ? ? ? ? ? 1 . 0 1 . 0 0 0
		// 05 ? ? ? ? ? 1 . 0 0 . 0 1 2
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x05;
		static string ReadResponse(ReadOnlySpan<byte> buffer) => Encoding.ASCII.GetString(buffer.Slice(6, 8));

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<string> GetOtherVersion1Async(CancellationToken cancellationToken)
	{
		// NB: This returns a different version number than the other ones.
		// I have no idea what this is about as I just found this using blackbox testing, but it is definitely a version.
		// 04 ? ? ? ? ? 0 . 0 1 . 0 0 7
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x05;
		static string ReadResponse(ReadOnlySpan<byte> buffer) => Encoding.ASCII.GetString(buffer.Slice(6, 8));

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<string> GetOtherVersion2Async(CancellationToken cancellationToken)
	{
		// NB: This returns a different version number than the other ones.
		// I have no idea what this is about as I just found this using blackbox testing, but it is definitely a version.
		// 04 ? ? ? ? ? 1 . 0 0 . 0 0 8
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x05;
		static string ReadResponse(ReadOnlySpan<byte> buffer) => Encoding.ASCII.GetString(buffer.Slice(6, 8));

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<uint> GetSleepTimerAsync(CancellationToken cancellationToken)
	{
		// NB: This command was manually tested to return the sleep timer (if we change it, the exact value is returned), along with an unkown byte value.
		// In my testing, the unknown byte was always `04`
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x0A;
		static uint ReadResponse(ReadOnlySpan<byte> buffer) => LittleEndian.ReadUInt32(in buffer[2]);

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task<ushort> GetUsageTimeAsync(CancellationToken cancellationToken)
	{
		// This command seems to return some status on the device.
		// I saw one of the values increasing at some point, which was not related to a key press or any specific action from the official software,  so the only explanation is that it is time.
		// Expectation was then that one of the value represented a 2byte or 4byte duration in hours for which the device was on.
		// Number returned matched closely the expected duration of my device.
		// Format: AA AA BB BB TT TT TT TT
		// Assuming that the duration is 4 bytes, although unproven. One would need to keep the device turned on 7.5 years to find out.
		// Other values are unknown. Were `04 00` and `02 00` on the day of measurement.

		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer) => buffer[0] = 0x0A;
		static ushort ReadResponse(ReadOnlySpan<byte> buffer) => LittleEndian.ReadUInt16(in buffer[2]);

		PrepareRequest(buffer.Span);
		await _stream.ReceiveFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
		return ReadResponse(buffer.Span);
	}

	public async Task SetBrightnessAsync(byte value, CancellationToken cancellationToken)
	{
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer, byte value)
		{
			buffer[0] = 0x03;
			buffer[1] = 0x08;
			buffer[2] = value;
		}

		PrepareRequest(buffer.Span, value);
		await _stream.SendFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
	}

	public async Task SetSleepTimerAsync(uint timeoutInSeconds, CancellationToken cancellationToken)
	{
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer, uint timeoutInSeconds)
		{
			buffer[0] = 0x03;
			buffer[1] = 0x0d;
			LittleEndian.Write(ref buffer[2], timeoutInSeconds);
		}

		PrepareRequest(buffer.Span, timeoutInSeconds);
		await _stream.SendFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
	}

	public async Task ResetAsync(CancellationToken cancellationToken)
	{
		var buffer = FeatureBuffer;

		static void PrepareRequest(Span<byte> buffer)
		{
			buffer[0] = 0x03;
			buffer[1] = 0x02;
		}

		PrepareRequest(buffer.Span);
		await _stream.SendFeatureReportAsync(buffer, cancellationToken).ConfigureAwait(false);
	}

	public Task SetKeyImageDataAsync(byte keyX, byte keyY, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		if (keyX >= _deviceInfo.ButtonRowCount) throw new ArgumentOutOfRangeException(nameof(keyX));
		if (keyY >= _deviceInfo.ButtonColumnCount) throw new ArgumentOutOfRangeException(nameof(keyY));

		return SetKeyImageDataAsync((byte)(_deviceInfo.ButtonRowCount * keyY + keyX), data, cancellationToken);
	}

	public async Task SetKeyImageDataAsync(byte keyIndex, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		if (keyIndex > _deviceInfo.ButtonCount) throw new ArgumentOutOfRangeException(nameof(keyIndex));

		var buffer = WriteBuffer;

		ushort maxSliceLength = checked((ushort)(buffer.Length - 8));

		static ReadOnlyMemory<byte> PrepareRequest(Span<byte> buffer, ushort maxSliceLength, byte keyIndex, ReadOnlyMemory<byte> remaining)
		{
			buffer[0] = 0x02;
			buffer[1] = 0x07;
			buffer[2] = keyIndex;

			return UpdateRequest(buffer, maxSliceLength, remaining, 0);
		}

		static ReadOnlyMemory<byte> UpdateRequest(Span<byte> buffer, ushort maxSliceLength, ReadOnlyMemory<byte> remaining, ushort index)
		{
			bool isLastSlice = remaining.Length <= maxSliceLength;
			ushort sliceLength = isLastSlice ? (ushort)remaining.Length : maxSliceLength;

			buffer[3] = isLastSlice ? (byte)0x01 : (byte)0x00;
			Unsafe.WriteUnaligned(ref buffer[4], BitConverter.IsLittleEndian ? sliceLength : BinaryPrimitives.ReverseEndianness(sliceLength));
			Unsafe.WriteUnaligned(ref buffer[6], BitConverter.IsLittleEndian ? index : BinaryPrimitives.ReverseEndianness(index));
			remaining.Span[..sliceLength].CopyTo(buffer[8..]);

			return remaining[sliceLength..];
		}

		var remaining = data;

		remaining = PrepareRequest(buffer.Span, maxSliceLength, keyIndex, remaining);
		ushort index = 0;
		while (true)
		{
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (remaining.Length == 0) break;
			remaining = UpdateRequest(buffer.Span, maxSliceLength, remaining, ++index);
		}
	}

	public async Task SetScreenSaverImageDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		var buffer = WriteBuffer;

		ushort maxSliceLength = checked((ushort)(buffer.Length - 8));

		static ReadOnlyMemory<byte> PrepareRequest(Span<byte> buffer, ushort maxSliceLength, ReadOnlyMemory<byte> remaining)
		{
			buffer[0] = 0x02;
			buffer[1] = 0x09;
			buffer[2] = 0x08;

			return UpdateRequest(buffer, maxSliceLength, remaining, 0);
		}

		static ReadOnlyMemory<byte> UpdateRequest(Span<byte> buffer, ushort maxSliceLength, ReadOnlyMemory<byte> remaining, ushort index)
		{
			bool isLastSlice = remaining.Length <= maxSliceLength;
			ushort sliceLength = isLastSlice ? (ushort)remaining.Length : maxSliceLength;

			// NB: For some reason, the slice index & length are inversed compared to the call to set the key image 🤷
			buffer[3] = isLastSlice ? (byte)0x01 : (byte)0x00;
			Unsafe.WriteUnaligned(ref buffer[4], BitConverter.IsLittleEndian ? index : BinaryPrimitives.ReverseEndianness(index));
			Unsafe.WriteUnaligned(ref buffer[6], BitConverter.IsLittleEndian ? sliceLength : BinaryPrimitives.ReverseEndianness(sliceLength));
			remaining.Span[..sliceLength].CopyTo(buffer[8..]);

			return remaining[sliceLength..];
		}

		var remaining = data;

		remaining = PrepareRequest(buffer.Span, maxSliceLength, remaining);
		ushort index = 0;
		while (true)
		{
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (remaining.Length == 0) break;
			remaining = UpdateRequest(buffer.Span, maxSliceLength, remaining, ++index);
		}
	}
}
