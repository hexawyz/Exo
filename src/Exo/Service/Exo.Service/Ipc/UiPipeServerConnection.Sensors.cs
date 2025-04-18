using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Primitives;
using Exo.Sensors;
using Exo.Settings.Ui.Ipc;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task WatchSensorDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in _sensorService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = WriteUpdate(buffer.Span, info);
					await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}

		static int WriteUpdate(Span<byte> buffer, in SensorDeviceInformation device)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.SensorDevice);
			writer.Write(device.DeviceId);
			if (device.Sensors.IsDefaultOrEmpty)
			{
				writer.Write((byte)0);
			}
			else
			{
				writer.WriteVariable((uint)device.Sensors.Length);
				foreach (var sensor in device.Sensors)
				{
					writer.Write(sensor.SensorId);
					writer.Write((byte)sensor.DataType);
					writer.Write((byte)sensor.Capabilities);
					writer.WriteVariableString(sensor.Unit);
					if ((sensor.Capabilities & SensorCapabilities.HasMinimumValue) != 0) Write(ref writer, sensor.DataType, sensor.ScaleMinimumValue);
					if ((sensor.Capabilities & SensorCapabilities.HasMaximumValue) != 0) Write(ref writer, sensor.DataType, sensor.ScaleMaximumValue);
				}
			}
			return (int)writer.Length;
		}

		static void Write(ref BufferWriter writer, SensorDataType dataType, VariantNumber value)
		{
			switch (dataType)
			{
			case SensorDataType.UInt8: writer.Write((byte)value); break;
			case SensorDataType.UInt16: writer.Write((ushort)value); break;
			case SensorDataType.UInt32: writer.Write((uint)value); break;
			case SensorDataType.UInt64: writer.Write((ulong)value); break;
			case SensorDataType.UInt128: writer.Write((UInt128)value); break;
			case SensorDataType.SInt8: goto case SensorDataType.UInt8;
			case SensorDataType.SInt16: goto case SensorDataType.UInt16;
			case SensorDataType.SInt32: goto case SensorDataType.UInt32;
			case SensorDataType.SInt64: goto case SensorDataType.UInt64;
			case SensorDataType.SInt128: goto case SensorDataType.UInt128;
			case SensorDataType.Float16: goto case SensorDataType.UInt16;
			case SensorDataType.Float32: goto case SensorDataType.UInt32;
			case SensorDataType.Float64: goto case SensorDataType.UInt64;
			default: throw new InvalidOperationException();
			}
		}
	}

	private async Task WatchSensorConfigurationUpdatesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var info in _sensorService.WatchSensorConfigurationChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = WriteUpdate(buffer.Span, info);
					await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}

		static int WriteUpdate(Span<byte> buffer, in SensorConfigurationUpdate update)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.SensorConfiguration);
			writer.Write(update.DeviceId);
			writer.Write(update.SensorId);
			writer.WriteVariableString(update.FriendlyName);
			writer.Write(update.IsFavorite);
			return (int)writer.Length;
		}
	}

	private async Task ProcessSensorFavoritingAsync(ChannelReader<SensorFavoritingRequest> reader, CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				while (reader.TryRead(out var update))
				{
					await _sensorService.SetFavoriteAsync(update.DeviceId, update.SensorId, update.IsFavorite, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task WatchSensorUpdates(ChannelReader<SensorUpdate> reader, CancellationToken cancellationToken)
	{
		while (true)
		{
			try
			{
				await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var buffer = WriteBuffer;
				while (reader.TryRead(out var update))
				{
					bool isStop = update.Length < 0;
					uint streamId = update.StreamId;
					int count = WriteUpdate(buffer.Span, update);
					try
					{
						await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						return;
					}
					if (update.Length < 0)
					{
						_sensorWatchStates.Remove(streamId);
					}
				}
			}
		}

		static int WriteUpdate(Span<byte> buffer, in SensorUpdate update)
		{
			var writer = new BufferWriter(buffer);
			if (update.Length < 0)
			{
				writer.Write((byte)ExoUiProtocolServerMessage.SensorStop);
				writer.WriteVariable(update.StreamId);
			}
			else
			{
				writer.Write((byte)ExoUiProtocolServerMessage.SensorValue);
				writer.WriteVariable(update.StreamId);
				writer.Write(SensorUpdate.GetData(in update));
			}
			return (int)writer.Length;
		}
	}

	private ValueTask<bool> ProcessSensorRequestAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		var deviceId = reader.ReadGuid();
		var sensorId = reader.ReadGuid();
		return ProcessSensorRequestAsync(streamId, deviceId, sensorId, cancellationToken);
	}

	private ValueTask WriteSensorStartStatusAsync(uint streamId, SensorStartStatus status, CancellationToken cancellationToken)
		=> UnsafeWriteSimpleOperationStatusAsync(ExoUiProtocolServerMessage.SensorStart, streamId, (byte)status, cancellationToken);

	private void ProcessSensorFavoriteRequest(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		var deviceId = reader.ReadGuid();
		var sensorId = reader.ReadGuid();
		bool isFavorite = reader.ReadBoolean();

		_sensorFavoritingChannel.Writer.TryWrite(new() { DeviceId = deviceId, SensorId = sensorId, IsFavorite = isFavorite });
	}

	private async ValueTask<bool> ProcessSensorRequestAsync(uint streamId, Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
	{
		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (_sensorWatchStates.ContainsKey(streamId))
			{
				await WriteSensorStartStatusAsync(streamId, SensorStartStatus.StreamIdAlreadyInUse, cancellationToken).ConfigureAwait(false);
				goto Success;
			}
			else
			{
				SensorDataType dataType;
				object enumerable;
				try
				{
					(dataType, enumerable) = await _sensorService.GetValueWatcherAsync(deviceId, sensorId, cancellationToken).ConfigureAwait(false);
				}
				catch (DeviceNotFoundException)
				{
					await WriteSensorStartStatusAsync(streamId, SensorStartStatus.DeviceNotFound, cancellationToken).ConfigureAwait(false);
					goto Success;
				}
				catch (SensorNotFoundException)
				{
					await WriteSensorStartStatusAsync(streamId, SensorStartStatus.SensorNotFound, cancellationToken).ConfigureAwait(false);
					goto Success;
				}
				SensorWatchState state;
				try
				{
					_sensorWatchStates.Add(streamId, state = SensorWatchState.Create(this, streamId, dataType, enumerable));
				}
				catch
				{
					await WriteSensorStartStatusAsync(streamId, SensorStartStatus.Error, cancellationToken).ConfigureAwait(false);
					goto Success;
				}
				await WriteSensorStartStatusAsync(streamId, SensorStartStatus.Success, cancellationToken).ConfigureAwait(false);
				_logger.UiSensorServiceSensorWatchStart(deviceId, sensorId, streamId);
				state.Start();
			}
		}
	Success:;
		return true;
	}

	private abstract class SensorWatchState : IAsyncDisposable
	{
		public static SensorWatchState Create(UiPipeServerConnection connection, uint streamId, SensorDataType dataType, object enumerable)
			=> dataType switch
			{
				SensorDataType.UInt8 => new SensorWatchState<byte>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<byte>>>(enumerable)),
				SensorDataType.UInt16 => new SensorWatchState<ushort>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<ushort>>>(enumerable)),
				SensorDataType.UInt32 => new SensorWatchState<uint>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<uint>>>(enumerable)),
				SensorDataType.UInt64 => new SensorWatchState<ulong>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<ulong>>>(enumerable)),
				SensorDataType.UInt128 => new SensorWatchState<UInt128>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<UInt128>>>(enumerable)),
				SensorDataType.SInt8 => new SensorWatchState<sbyte>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<sbyte>>>(enumerable)),
				SensorDataType.SInt16 => new SensorWatchState<short>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<short>>>(enumerable)),
				SensorDataType.SInt32 => new SensorWatchState<int>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<int>>>(enumerable)),
				SensorDataType.SInt64 => new SensorWatchState<long>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<long>>>(enumerable)),
				SensorDataType.SInt128 => new SensorWatchState<Int128>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<Int128>>>(enumerable)),
				SensorDataType.Float16 => new SensorWatchState<Half>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<Half>>>(enumerable)),
				SensorDataType.Float32 => new SensorWatchState<float>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<float>>>(enumerable)),
				SensorDataType.Float64 => new SensorWatchState<double>(connection, streamId, Unsafe.As<IAsyncEnumerable<SensorDataPoint<double>>>(enumerable)),
				_ => throw new ArgumentOutOfRangeException(nameof(dataType)),
			};

		private readonly UiPipeServerConnection _connection;
		private readonly uint _streamId;
		private CancellationTokenSource? _cancellationTokenSource;

		protected SensorWatchState(UiPipeServerConnection connection, uint streamId)
		{
			_connection = connection;
			_streamId = streamId;
			_cancellationTokenSource = new();
		}

		protected UiPipeServerConnection Connection => _connection;
		protected uint StreamId => _streamId;
		protected CancellationToken CancellationToken => _cancellationTokenSource!.Token;

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				try { await DisposeOnceAsync(cts.Token).ConfigureAwait(false); }
				catch { }
				cts.Dispose();
			}
		}

		protected abstract ValueTask DisposeOnceAsync(CancellationToken canceledToken);

		public abstract void Start();
	}

	private sealed class SensorWatchState<TValue> : SensorWatchState
		where TValue : unmanaged, INumber<TValue>
	{
		private TaskCompletionSource? _taskCompletionSource;
		private readonly Task _task;

		public SensorWatchState(UiPipeServerConnection connection, uint streamId, IAsyncEnumerable<SensorDataPoint<TValue>> enumerable)
			: base(connection, streamId)
		{
			_taskCompletionSource = new();
			_task = Connection._logger.IsEnabled(LogLevel.Trace) ?
				WatchValuesWithLoggingAsync(enumerable, connection._sensorUpdateChannel.Writer, CancellationToken) :
				WatchValuesAsync(enumerable, connection._sensorUpdateChannel.Writer, CancellationToken);
		}

		protected override ValueTask DisposeOnceAsync(CancellationToken canceledToken)
		{
			Interlocked.Exchange(ref _taskCompletionSource, null)?.TrySetCanceled(canceledToken);
			return new(_task);
		}

		public override void Start() => Interlocked.Exchange(ref _taskCompletionSource, null)?.TrySetResult();

		// A version of the watcher that will log received values.
		private async Task WatchValuesWithLoggingAsync(IAsyncEnumerable<SensorDataPoint<TValue>> enumerable, ChannelWriter<SensorUpdate> writer, CancellationToken cancellationToken)
		{
			await _taskCompletionSource!.Task.ConfigureAwait(false);
			try
			{
				try
				{
					await foreach (var value in enumerable.ConfigureAwait(false))
					{
						writer.TryWrite(SensorUpdate.Create(StreamId, value.Value));
						Connection._logger.UiSensorServiceSensorWatchNotification(StreamId, value.DateTime, value.Value);
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				// We want to notify the client of the stream end.
				// Calling this helper method will be the simplest way for now, as it avoids dragging along the connection's cancellation token.
				if (Connection.TryGetDefaultWriteCancellationToken(out var writeCancellationToken))
				{
					writer.TryWrite(SensorUpdate.CreateEndOfStream(StreamId));
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Connection._logger.UiSensorServiceSensorWatchError(StreamId, ex);
			}
			finally
			{
				Connection._logger.UiSensorServiceSensorWatchStop(StreamId);
			}
		}

		private async Task WatchValuesAsync(IAsyncEnumerable<SensorDataPoint<TValue>> enumerable, ChannelWriter<SensorUpdate> writer, CancellationToken cancellationToken)
		{
			await _taskCompletionSource!.Task.ConfigureAwait(false);
			try
			{
				try
				{
					await foreach (var value in enumerable.ConfigureAwait(false))
					{
						writer.TryWrite(SensorUpdate.Create(StreamId, value.Value));
						Connection._logger.UiSensorServiceSensorWatchNotification(StreamId, value.DateTime, value.Value);
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				// We want to notify the client of the stream end.
				// Calling this helper method will be the simplest way for now, as it avoids dragging along the connection's cancellation token.
				if (Connection.TryGetDefaultWriteCancellationToken(out var writeCancellationToken))
				{
					writer.TryWrite(SensorUpdate.CreateEndOfStream(StreamId));
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Connection._logger.UiSensorServiceSensorWatchError(StreamId, ex);
			}
			finally
			{
				Connection._logger.UiSensorServiceSensorWatchStop(StreamId);
			}
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private readonly struct SensorUpdate
	{
		[SkipLocalsInit]
		public static SensorUpdate CreateEndOfStream(uint streamId)
		{
			var update = new SensorUpdate();
			Unsafe.AsRef(in update.StreamId) = streamId;
			Unsafe.AsRef(in update.Length) = -1;
			return update;
		}

		[SkipLocalsInit]
		public static SensorUpdate Create<TValue>(uint streamId, TValue value)
			where TValue : unmanaged, INumber<TValue>
		{
			var update = new SensorUpdate();
			Unsafe.AsRef(in update.StreamId) = streamId;
			Unsafe.AsRef(in update.Length) = Unsafe.SizeOf<TValue>();
			Unsafe.As<byte, TValue>(ref Unsafe.AsRef(in update._data0)) = value;
			return update;
		}

		public static ReadOnlySpan<byte> GetData(scoped in SensorUpdate update)
			=> MemoryMarshal.CreateReadOnlySpan(in update._data0, update.Length);

		// < 0 if stream end
		public readonly uint StreamId;
		public readonly int Length;
		private readonly byte _data0;
		private readonly byte _data1;
		private readonly byte _data2;
		private readonly byte _data3;
		private readonly byte _data4;
		private readonly byte _data5;
		private readonly byte _data6;
		private readonly byte _data7;
		private readonly byte _data8;
		private readonly byte _data9;
		private readonly byte _dataA;
		private readonly byte _dataB;
		private readonly byte _dataC;
		private readonly byte _dataD;
		private readonly byte _dataE;
		private readonly byte _dataF;
	}

	private readonly struct SensorFavoritingRequest
	{
		public Guid DeviceId { get; init; }
		public Guid SensorId { get; init; }
		public bool IsFavorite { get; init; }
	}
}
