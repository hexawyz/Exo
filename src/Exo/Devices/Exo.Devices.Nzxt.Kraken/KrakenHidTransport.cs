using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Nzxt.Kraken;

internal sealed class KrakenHidTransport : IAsyncDisposable
{
	// The message length is 64 bytes including the report ID, which indicates a specific command.
	private const int MessageLength = 64;

	private const byte ScreenSettingsRequestMessageId = 0x30;
	private const byte ScreenSettingsResponseMessageId = 0x31;
	private const byte ImageMemoryManagementRequestMessageId = 0x32;
	private const byte ImageMemoryManagementResponseMessageId = 0x33;
	private const byte DisplayChangeRequestMessageId = 0x38;
	private const byte CoolingPowerRequestMessageId = 0x72;
	private const byte DeviceStatusRequestMessageId = 0x74;
	private const byte DeviceStatusResponseMessageId = 0x75;
	private const byte GenericResponseMessageId = 0xFF;

	private const byte ScreenSettingsGetScreenInfoFunctionId = 0x01;
	private const byte ScreenSettingsSetBrightnessFunctionId = 0x02;

	private const byte CoolingPowerPumpFunctionId = 0x01;
	private const byte CoolingPowerFanFunctionId = 0x02;

	private const byte CurrentDeviceStatusFunctionId = 0x01;

	private readonly HidFullDuplexStream _stream;
	private readonly byte[] _buffers;
	private ulong _lastReadings;
	private ulong _lastReadingsTimestamp;
	// In this driver, we allow multiple in-flight requests, as long as they are on different functions.
	// However, we don't want to waste memory by allocating more than one write buffer, so we want to serialize writes to the device.
	// Anyway, operations will have low-to-no contention, and sending parallel writes would have no value, as the writes will end up being serialized by the HID stack anyway.
	private readonly AsyncLock _writeLock;
	// In order to support concurrent different operations, we need to have one specific TaskCompletionSource field for each operation.
	private TaskCompletionSource? _setBrightnessTaskCompletionSource;
	private TaskCompletionSource? _setDisplayModeTaskCompletionSource;
	private TaskCompletionSource<ScreenInformation>? _screenInfoRetrievalTaskCompletionSource;
	private TaskCompletionSource? _setPumpPowerTaskCompletionSource;
	private TaskCompletionSource? _setFanPowerTaskCompletionSource;
	private TaskCompletionSource? _statusRetrievalTaskCompletionSource;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _task;

	public KrakenHidTransport(HidFullDuplexStream stream)
	{
		_stream = stream;
		_buffers = GC.AllocateUninitializedArray<byte>(2 * MessageLength, true);
		_writeLock = new();
		_cancellationTokenSource = new();
		_task = ReadAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		cts.Cancel();
		await _stream.DisposeAsync().ConfigureAwait(false);
		await _task.ConfigureAwait(false);
		var objectDisposedException = ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(KrakenHidTransport).FullName));
		_setBrightnessTaskCompletionSource?.TrySetException(objectDisposedException);
		_screenInfoRetrievalTaskCompletionSource?.TrySetException(objectDisposedException);
		_setPumpPowerTaskCompletionSource?.TrySetException(objectDisposedException);
		_setFanPowerTaskCompletionSource?.TrySetException(objectDisposedException);
		cts.Dispose();
	}

	private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _cancellationTokenSource) is null, typeof(KrakenHidTransport));

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

	private static Task WaitOrCancelAsync(TaskCompletionSource taskCompletionSource, CancellationToken cancellationToken)
	{
		return cancellationToken.CanBeCanceled ? WaitWithCancellationAsync(taskCompletionSource, cancellationToken) : taskCompletionSource.Task;

		static async Task WaitWithCancellationAsync(TaskCompletionSource taskCompletionSource, CancellationToken cancellationToken)
		{
			await using (var registration = cancellationToken.UnsafeRegister(taskCompletionSource).ConfigureAwait(false))
			{
				await taskCompletionSource.Task.ConfigureAwait(false);
			}
		}
	}

	private static Task<T> WaitOrCancelAsync<T>(TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken)
	{
		return cancellationToken.CanBeCanceled ? WaitWithCancellationAsync(taskCompletionSource, cancellationToken) : taskCompletionSource.Task;

		static async Task<T> WaitWithCancellationAsync(TaskCompletionSource<T> taskCompletionSource, CancellationToken cancellationToken)
		{
			await using (var registration = cancellationToken.UnsafeRegister(taskCompletionSource).ConfigureAwait(false))
			{
				return await taskCompletionSource.Task.ConfigureAwait(false);
			}
		}
	}

	private Memory<byte> WriteBuffer => MemoryMarshal.CreateFromPinnedArray(_buffers, MessageLength, MessageLength);

	public async ValueTask<ScreenInformation> GetScreenInformationAsync(CancellationToken cancellationToken)
	{
		EnsureNotDisposed();

		var tcs = new TaskCompletionSource<ScreenInformation>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _screenInfoRetrievalTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer)
		{
			// NB: Write buffer is assumed to be cleared from index 2, and this part should always be cleared before releasing the write lock.
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = ScreenSettingsGetScreenInfoFunctionId;
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				PrepareRequest(buffer.Span);
				await _stream.WriteAsync(buffer, default).ConfigureAwait(false);
			}
			return await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _screenInfoRetrievalTaskCompletionSource, null);
		}
	}

	public async ValueTask SetBrightnessAsync(byte brightness, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(brightness, 100);

		EnsureNotDisposed();

		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _setBrightnessTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer, byte brightness)
		{
			// NB: Write buffer is assumed to be cleared from index 2, and this part should always be cleared before releasing the write lock.
			buffer[0] = ScreenSettingsRequestMessageId;
			buffer[1] = ScreenSettingsSetBrightnessFunctionId;
			buffer[2] = 0x01;
			buffer[3] = brightness;
			buffer[7] = 0x03;
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					PrepareRequest(buffer.Span, brightness);
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					buffer.Span[2..8].Clear();
				}
			}
			await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _setBrightnessTaskCompletionSource, null);
		}
	}

	public async ValueTask DisplayImageAsync(byte index, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 0);
		EnsureNotDisposed();

		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _setDisplayModeTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer, byte imageIndex)
		{
			buffer.Clear();
			buffer[0] = DisplayChangeRequestMessageId;
			buffer[1] = 0x01;
			buffer[2] = 4;
			buffer[3] = imageIndex;
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					PrepareRequest(buffer.Span, index);
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					buffer.Span[2..8].Clear();
				}
			}
			await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _setDisplayModeTaskCompletionSource, null);
		}
	}

	public async ValueTask DisplayPresetVisualAsync(KrakenPresetVisual visual, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)visual, 3, nameof(visual));
		EnsureNotDisposed();

		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _setDisplayModeTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer, KrakenPresetVisual visual)
		{
			buffer.Clear();
			buffer[0] = DisplayChangeRequestMessageId;
			buffer[1] = 0x01;
			buffer[2] = (byte)visual;
			buffer[3] = 0;
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					PrepareRequest(buffer.Span, visual);
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					buffer.Span[2..8].Clear();
				}
			}
			await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref _setDisplayModeTaskCompletionSource, null);
		}
	}

	public ValueTask SetPumpPowerAsync(byte power, CancellationToken cancellationToken)
		=> SetPowerAsync(CoolingPowerPumpFunctionId, 0x00, power, cancellationToken);

	public ValueTask SetFanPowerAsync(byte power, CancellationToken cancellationToken)
		=> SetPowerAsync(CoolingPowerFanFunctionId, 0x01, power, cancellationToken);

	private async ValueTask SetPowerAsync(byte functionId, byte parameter, byte power, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(power, 100);

		EnsureNotDisposed();

		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref functionId == CoolingPowerPumpFunctionId ? ref _setPumpPowerTaskCompletionSource : ref _setFanPowerTaskCompletionSource, tcs, null) is not null)
		{
			throw new InvalidOperationException();
		}

		static void PrepareRequest(Span<byte> buffer, byte functionId, byte parameter, byte power)
		{
			// NB: Write buffer is assumed to be cleared from index 2, and this part should always be cleared before releasing the write lock.
			buffer[0] = CoolingPowerRequestMessageId;
			buffer[1] = functionId;
			buffer[2] = parameter;
			buffer[3] = power;
			buffer.Slice(4, 40).Fill(power);
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					PrepareRequest(buffer.Span, functionId, parameter, power);
					await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					buffer.Span[2..44].Clear();
				}
			}
			await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			Volatile.Write(ref functionId == CoolingPowerPumpFunctionId ? ref _setPumpPowerTaskCompletionSource : ref _setFanPowerTaskCompletionSource, null);
		}
	}

	/// <summary>Gets the readings from a recent read result or a new query./summary>
	/// <remarks>
	/// Another software could already be querying the hardware, and we will see the results of these query, so we can avoid multiplying status queries on the hardware.
	/// To do this, we keep track of the time at which the last query results were received, and only do a new query if the results are too old.
	/// The only downside of this approach is that there will be a semi-random latency on read values, up to the 1s max delay that we allow.
	/// </remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public ValueTask<KrakenReadings> GetRecentReadingsAsync(CancellationToken cancellationToken)
	{
		EnsureNotDisposed();

		// If another software is running and already querying the hardware, we avoid generating a new query and reuse the result of a recent read.
		if ((ulong)Stopwatch.GetTimestamp() - _lastReadingsTimestamp < (ulong)Stopwatch.Frequency)
		{
			return new(GetLastReadings());
		}

		return new(GetCurrentReadingsAsync(cancellationToken));
	}

	public async Task<KrakenReadings> GetCurrentReadingsAsync(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (Interlocked.CompareExchange(ref _statusRetrievalTaskCompletionSource, tcs, null) is not null) throw new InvalidOperationException();

		static void PrepareRequest(Span<byte> buffer)
		{
			// NB: Write buffer is assumed to be cleared from index 2, and this part should always be cleared before releasing the write lock.
			buffer[0] = DeviceStatusRequestMessageId;
			buffer[1] = 01;
		}

		var buffer = WriteBuffer;
		try
		{
			using (await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				PrepareRequest(buffer.Span);
				await _stream.WriteAsync(buffer, default).ConfigureAwait(false);
			}
			await WaitOrCancelAsync(tcs, cancellationToken).ConfigureAwait(false);
			return GetLastReadings();
		}
		finally
		{
			Volatile.Write(ref _statusRetrievalTaskCompletionSource, null);
		}
	}

	private void ProcessReadMessage(ReadOnlySpan<byte> message)
		=> ProcessReadMessage(message[0], message[1], message[14..]);

	private void ProcessReadMessage(byte messageId, byte functionId, ReadOnlySpan<byte> data)
	{
		switch (messageId)
		{
		case DeviceStatusResponseMessageId:
			ProcessDeviceStatusResponse(functionId, data);
			break;
		case ScreenSettingsResponseMessageId:
			ProcessScreenInformationResponse(functionId, data);
			break;
		case GenericResponseMessageId:
			ProcessGenericResponse(data);
			break;
		}
	}

	private void ProcessDeviceStatusResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == CurrentDeviceStatusFunctionId)
		{
			byte liquidTemperature = response[1];
			ushort pumpSpeed = LittleEndian.ReadUInt16(in response[3]);
			byte pumpSetPower = response[19];
			ushort fanSpeed = LittleEndian.ReadUInt16(in response[9]);
			byte fanSetPower = response[11];
			var readings = new KrakenReadings(pumpSpeed, fanSpeed, pumpSetPower, fanSetPower, liquidTemperature);
			Volatile.Write(ref _lastReadings, Unsafe.BitCast<KrakenReadings, ulong>(readings));
			Volatile.Write(ref _lastReadingsTimestamp, (ulong)Stopwatch.GetTimestamp());

			_statusRetrievalTaskCompletionSource?.TrySetResult();
		}
	}

	private void ProcessScreenInformationResponse(byte functionId, ReadOnlySpan<byte> response)
	{
		if (functionId == ScreenSettingsGetScreenInfoFunctionId)
		{
			byte imageCount = response[5];
			ushort imageWidth = LittleEndian.ReadUInt16(in response[6]);
			ushort imageHeight = LittleEndian.ReadUInt16(in response[8]);
			byte brightness = response[10];

			_screenInfoRetrievalTaskCompletionSource?.TrySetResult(new(brightness, imageCount, imageWidth, imageHeight));
		}
	}

	private void ProcessGenericResponse(ReadOnlySpan<byte> response)
		=> ProcessGenericResponse(response[0], response[1]);

	private void ProcessGenericResponse(byte messageId, byte functionId)
		=>
		(
			messageId switch
			{
				ScreenSettingsRequestMessageId => functionId switch
				{
					ScreenSettingsSetBrightnessFunctionId => _setBrightnessTaskCompletionSource,
					_ => null,
				},
				CoolingPowerRequestMessageId => functionId switch
				{
					CoolingPowerPumpFunctionId => _setPumpPowerTaskCompletionSource,
					CoolingPowerFanFunctionId => _setFanPowerTaskCompletionSource,
					_ => null,
				},
				_ => null,
			}
		)?.TrySetResult();

	public KrakenReadings GetLastReadings()
		=> Unsafe.BitCast<ulong, KrakenReadings>(Volatile.Read(ref _lastReadings));
}
