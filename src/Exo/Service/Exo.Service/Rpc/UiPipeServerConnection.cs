using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Primitives;
using Exo.Rpc;
using Exo.Sensors;

namespace Exo.Service.Rpc;

internal sealed class UiPipeServerConnection : PipeServerConnection, IPipeServerConnection<UiPipeServerConnection>
{
	private static readonly UnboundedChannelOptions SensorChannelOptions = new() { SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = true };

	public static UiPipeServerConnection Create(PipeServer<UiPipeServerConnection> server, NamedPipeServerStream stream)
	{
		var uiPipeServer = (UiPipeServer)server;
		return new(server, stream, uiPipeServer.ConnectionLogger, uiPipeServer.MetadataSourceProvider, uiPipeServer.CustomMenuService, uiPipeServer.SensorService);
	}

	private readonly IMetadataSourceProvider _metadataSourceProvider;
	private readonly CustomMenuService _customMenuService;
	private readonly SensorService _sensorService;
	private int _state;
	private readonly Dictionary<uint, SensorWatchState> _sensorWatchStates;
	private readonly Channel<SensorUpdate> _sensorUpdateChannel;
	private readonly ILogger<UiPipeServerConnection> _logger;

	private UiPipeServerConnection
	(
		PipeServer server,
		NamedPipeServerStream stream,
		ILogger<UiPipeServerConnection> logger,
		IMetadataSourceProvider metadataSourceProvider,
		CustomMenuService customMenuService,
		SensorService sensorService
	) : base(server, stream)
	{
		_logger = logger;
		_metadataSourceProvider = metadataSourceProvider;
		_customMenuService = customMenuService;
		_sensorService = sensorService;
		using (var callingProcess = Process.GetProcessById(NativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle)))
		{
			if (callingProcess.ProcessName != "Exo.Settings.Ui")
			{
				throw new UnauthorizedAccessException("The client is not authorized.");
			}
		}
		_sensorWatchStates = new();
		_sensorUpdateChannel = Channel.CreateUnbounded<SensorUpdate>(SensorChannelOptions);
	}

	protected override ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

	private async Task WatchEventsAsync(CancellationToken cancellationToken)
	{
		var metadataWatchTask = WatchMetadataChangesAsync(cancellationToken);
		var customMenuWatchTask = WatchCustomMenuChangesAsync(cancellationToken);
		var sensorDeviceWatchTask = WatchSensorDevicesAsync(cancellationToken);
		var sensorWatchTask = WatchSensorUpdates(_sensorUpdateChannel.Reader, cancellationToken);

		await Task.WhenAll(customMenuWatchTask, sensorDeviceWatchTask, sensorWatchTask).ConfigureAwait(false);
	}

	private async Task WatchMetadataChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _metadataSourceProvider.WatchMetadataSourceChangesAsync(cancellationToken))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = WriteNotification(buffer.Span, notification);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}
			}

			static int WriteNotification(Span<byte> buffer, MetadataSourceChangeNotification notification)
			{
				var writer = new BufferWriter(buffer);
				switch (notification.NotificationKind)
				{
				case WatchNotificationKind.Enumeration:
					writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesEnumeration);
					break;
				case WatchNotificationKind.Addition:
					writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesAdd);
					break;
				case WatchNotificationKind.Removal:
					writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesRemove);
					break;
				case WatchNotificationKind.Update:
					writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesUpdate);
					goto Completed;
				default: throw new UnreachableException();
				}
				WriteSources(ref writer, notification.Sources);
			Completed:;
				return (int)writer.Length;
			}

			static int WriteInitialization(Span<byte> buffer, ImmutableArray<MetadataSourceInformation> sources)
			{
				var writer = new BufferWriter(buffer);
				writer.Write((byte)ExoUiProtocolServerMessage.MetadataSourcesEnumeration);
				WriteSources(ref writer, sources);
				return (int)writer.Length;
			}

			static void WriteSources(ref BufferWriter writer, ImmutableArray<MetadataSourceInformation> sources)
			{
				writer.WriteVariable((uint)sources.Length);
				for (int i = 0; i < sources.Length; i++)
				{
					WriteSource(ref writer, sources[i]);
				}
			}

			static void WriteSource(ref BufferWriter writer, MetadataSourceInformation source)
			{
				writer.Write((byte)source.Category);
				writer.WriteVariableString(source.ArchivePath);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task WatchCustomMenuChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _customMenuService.WatchChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				var message = notification.Kind switch
				{
					WatchNotificationKind.Enumeration => ExoUiProtocolServerMessage.CustomMenuItemEnumeration,
					WatchNotificationKind.Addition => ExoUiProtocolServerMessage.CustomMenuItemAdd,
					WatchNotificationKind.Removal => ExoUiProtocolServerMessage.CustomMenuItemRemove,
					WatchNotificationKind.Update => ExoUiProtocolServerMessage.CustomMenuItemUpdate,
					_ => throw new UnreachableException()
				};
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = FillBuffer(buffer.Span, message, notification);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}

				static int FillBuffer(Span<byte> buffer, ExoUiProtocolServerMessage message, MenuItemWatchNotification notification)
				{
					buffer[0] = (byte)message;
					return WriteNotificationData(buffer[1..], notification) + 1;
				}

				static int WriteNotificationData(Span<byte> buffer, MenuItemWatchNotification notification)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(notification.ParentItemId);
					writer.Write(notification.Position);
					writer.Write(notification.MenuItem.ItemId);
					writer.Write((byte)notification.MenuItem.Type);
					if (notification.MenuItem.Type is Contracts.Ui.MenuItemType.Default or Contracts.Ui.MenuItemType.SubMenu)
					{
						writer.WriteVariableString((notification.MenuItem as TextMenuItem)?.Text ?? "");
					}
					return (int)writer.Length;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

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
					await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
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
			writer.WriteVariable(device.Sensors.IsDefault ? 0 : (uint)device.Sensors.Length);
			foreach (var sensor in device.Sensors)
			{
				writer.Write(sensor.SensorId);
				writer.Write((byte)sensor.DataType);
				writer.Write((byte)sensor.Capabilities);
				writer.WriteVariableString(sensor.Unit);
				if ((sensor.Capabilities & SensorCapabilities.HasMinimumValue) != 0) Write(ref writer, sensor.DataType, sensor.ScaleMinimumValue);
				if ((sensor.Capabilities & SensorCapabilities.HasMaximumValue) != 0) Write(ref writer, sensor.DataType, sensor.ScaleMaximumValue);
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

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// This should act as the handshake.
		try
		{
			await SendGitCommitIdAsync(cancellationToken).ConfigureAwait(false);
			using (var watchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				Task? watchTask = null;
				{
					int count = await stream.ReadAsync(buffer, watchCancellationTokenSource.Token).ConfigureAwait(false);
					if (count == 0) return;
					if (!await ProcessMessageAsync(buffer.Span[..count], watchCancellationTokenSource.Token).ConfigureAwait(false)) return;
					if (_state > 0) watchTask = WatchEventsAsync(watchCancellationTokenSource.Token);
				}

				try
				{
					while (true)
					{
						int count = await stream.ReadAsync(buffer, watchCancellationTokenSource.Token).ConfigureAwait(false);
						if (count == 0) return;
						// Ignore all messages if the state is negative (it means that something wrong happened, likely that the SHA1 don't match)
						if (_state < 0) continue;
						// If the message processing does not indicate success, we can close the connection.
						if (!await ProcessMessageAsync(buffer.Span[..count], watchCancellationTokenSource.Token).ConfigureAwait(false)) return;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
				finally
				{
					watchCancellationTokenSource.Cancel();
					if (watchTask is not null) await watchTask.ConfigureAwait(false);
				}
			}
		}
		finally
		{
			foreach (var sensorWatchState in _sensorWatchStates.Values)
			{
				await sensorWatchState.DisposeAsync();
			}
		}
	}

	private Task SendGitCommitIdAsync(CancellationToken cancellationToken)
		=> Program.GitCommitId.IsDefault ?
			SendGitCommitIdAsync(ImmutableCollectionsMarshal.AsImmutableArray(new byte[20]), cancellationToken) :
			SendGitCommitIdAsync(Program.GitCommitId, cancellationToken);

	private async Task SendGitCommitIdAsync(ImmutableArray<byte> version, CancellationToken cancellationToken)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoUiProtocolServerMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private ValueTask<bool> ProcessMessageAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
		=> ProcessMessageAsync((ExoUiProtocolClientMessage)data[0], data[1..], cancellationToken);

	private ValueTask<bool> ProcessMessageAsync(ExoUiProtocolClientMessage message, ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		if (_state == 0 && message != ExoUiProtocolClientMessage.GitVersion) goto Failure;
		switch (message)
		{
		case ExoUiProtocolClientMessage.NoOp:
			goto Success;
		case ExoUiProtocolClientMessage.GitVersion:
			if (data.Length != 20) goto Failure;
			_state = Program.GitCommitId.IsDefaultOrEmpty || !data.SequenceEqual(ImmutableCollectionsMarshal.AsArray(Program.GitCommitId)!) ? -1 : 1;
			goto Success;
		case ExoUiProtocolClientMessage.InvokeMenuCommand:
			if (data.Length != 16) goto Failure;
			ProcessMenuItemInvocation(Unsafe.ReadUnaligned<Guid>(in data[0]));
			goto Success;
		case ExoUiProtocolClientMessage.UpdateSettings:
			goto Success;
		case ExoUiProtocolClientMessage.SensorStart:
			return ProcessSensorRequestAsync(data, cancellationToken);
		}
	Success:;
		return new(true);
	Failure:;
		return new(false);
	}

	private void ProcessMenuItemInvocation(Guid commandId)
	{
	}

	private ValueTask<bool> ProcessSensorRequestAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		var deviceId = reader.ReadGuid();
		var sensorId = reader.ReadGuid();
		return ProcessSensorRequestAsync(streamId, deviceId, sensorId, cancellationToken);
	}

	private async ValueTask WriteSensorStartStatusAsync(uint streamId, SensorStartStatus status, CancellationToken cancellationToken)
	{
		var buffer = WriteBuffer;
		nuint length = Write(buffer.Span, streamId, status);
		await WriteAsync(buffer[..(int)length], cancellationToken);

		static nuint Write(Span<byte> buffer, uint streamId, SensorStartStatus status)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.SensorStart);
			writer.WriteVariable(streamId);
			writer.Write((byte)status);
			return writer.Length;
		}
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
}
