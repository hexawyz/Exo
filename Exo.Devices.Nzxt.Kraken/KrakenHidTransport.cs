using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Nzxt.Kraken;

internal sealed class KrakenHidTransport : IAsyncDisposable
{
	// The message length is 64 bytes including the report ID, which indicates a specific command.
	private const int MessageLength = 64;

	private const byte ScreenSettingsRequestMessageId = 0x30;
	private const byte ScreenSettingsResponseMessageId = 0x31;
	private const byte DisplayChangeRequestMessageId = 0x38;
	private const byte CurrentDeviceStatusResponseMessageId = 0x75;

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private ulong _lastReadings;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _task;
	private TaskCompletionSource<ScreenInformation>? _screenInfoRetrievalTaskCompletionSource;

	public KrakenHidTransport(HidFullDuplexStream stream)
	{
		_stream = stream;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_cancellationTokenSource = new();
		_task = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		cts.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _task.ConfigureAwait(false);
		cts.Dispose();
	}

	private async Task ReadAsync(CancellationToken cancellationToken)
	{
		// NB: In this initial version, we passively receive readings, because we let the external software handle everything.
		// As far as readings are concerned, we may want to keep a decorrelation between request and response anyway. This would likely allow to work more gracefully with other software.
		try
		{
			var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, 0, MessageLength);
			while (true)
			{
				try
				{
					// Data is received in fixed length packets, so we expect to always receive exactly the number of bytes that the buffer can hold.
					int count = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
					if (count == 0) return;
					if (count != buffer.Length) throw new InvalidOperationException();
				}
				catch (OperationCanceledException)
				{
					return;
				}

				ProcessReadMessage(buffer.Span);
			}
		}
		catch
		{
			// TODO: Log
		}
	}

	public async ValueTask<ScreenInformation> GetScreenInformationAsync(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<ScreenInformation>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _screenInfoRetrievalTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer)
		{
			buffer.Clear();
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = 0x01;
		}

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		PrepareRequest(buffer.Span);
		await _stream.WriteAsync(buffer, default).ConfigureAwait(false);
		try
		{
			return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _screenInfoRetrievalTaskCompletionSource, null);
		}
	}

	public async ValueTask SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		static void PrepareRequest(Span<byte> buffer, byte brightness)
		{
			buffer.Clear();
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = 0x02;
			buffer[2] = 0x01;
			buffer[3] = brightness;
			buffer[7] = 0x03;
		}

		ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);

		var buffer = MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);
		PrepareRequest(buffer.Span, brightness);
		await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
	{
		switch (message[0])
		{
		case CurrentDeviceStatusResponseMessageId:
			ProcessDeviceStatusResponse(message[1], message[14..]);
			break;
		case ScreenSettingsResponseMessageId:
			ProcessScreenInformationResponse(message[1], message[14..]);
			break;
		}
	}

	private void ProcessDeviceStatusResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == 0x01)
		{
			byte liquidTemperature = response[1];
			ushort pumpSpeed = LittleEndian.ReadUInt16(in response[3]);
			byte pumpSetPower = response[19];
			ushort fanSpeed = LittleEndian.ReadUInt16(in response[9]);
			byte fanSetPower = response[11];
			var readings = new KrakenReadings(pumpSpeed, fanSpeed, pumpSetPower, fanSetPower, liquidTemperature);
			Volatile.Write(ref _lastReadings, Unsafe.BitCast<KrakenReadings, ulong>(readings));
		}
	}

	private void ProcessScreenInformationResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == 0x01)
		{
			byte imageCount = response[5];
			ushort imageWidth = LittleEndian.ReadUInt16(in response[6]);
			ushort imageHeight = LittleEndian.ReadUInt16(in response[8]);
			byte brightness = response[10];

			_screenInfoRetrievalTaskCompletionSource?.TrySetResult(new(brightness, imageCount, imageWidth, imageHeight));
		}
	}

	public KrakenReadings GetLastReadings()
		=> Unsafe.BitCast<ulong, KrakenReadings>(Volatile.Read(ref _lastReadings));
}
