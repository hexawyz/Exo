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
	private readonly struct DeviceInfo
	{
		public DeviceInfo(byte gridWidth, byte gridHeight)
		{
			GridWidth = gridWidth;
			GridHeight = gridHeight;
		}

		public byte GridWidth { get; }
		public byte GridHeight { get; }
	}

	private static readonly Dictionary<ushort, DeviceInfo> DeviceInformations = new()
	{
		{ 0x0060, new(5, 3) },
		{ 0x0063, new(3, 2) },
		{ 0x006C, new(8, 4) },
		{ 0x006D, new(3, 2) },
	};

	private const int WriteBufferLength = 1024;
	private const int ReadBufferLength = 512;
	private const int FeatureBufferLength = 32;

	private readonly HidFullDuplexStream _stream;
	private readonly DeviceInfo _deviceInfo;
	private readonly byte[] _ioBuffers;

	public StreamDeckDevice(HidFullDuplexStream stream, ushort productId)
	{
		_stream = stream;
		_deviceInfo = DeviceInformations[productId];
		_ioBuffers = GC.AllocateUninitializedArray<byte>(WriteBufferLength + ReadBufferLength + FeatureBufferLength, pinned: true);
	}

	public async ValueTask DisposeAsync()
	{
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	private Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, 0, WriteBufferLength);
	private Memory<byte> ReadBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, WriteBufferLength, ReadBufferLength);
	private Memory<byte> FeatureBuffer => MemoryMarshal.CreateFromPinnedArray(_ioBuffers, WriteBufferLength + ReadBufferLength, FeatureBufferLength);

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
			Unsafe.WriteUnaligned(ref buffer[2], BitConverter.IsLittleEndian ? timeoutInSeconds : BinaryPrimitives.ReverseEndianness(timeoutInSeconds));
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

	public Task SetKeyRawImageAsync(byte keyX, byte keyY, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
		=> SetKeyRawImageAsync((byte)(_deviceInfo.GridWidth * keyY + keyX), data, cancellationToken);

	public async Task SetKeyRawImageAsync(byte keyIndex, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
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
}
