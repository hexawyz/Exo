using System.Collections.Immutable;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Settings;
using Exo.Primitives;
using Exo.Rpc;
using Exo.Settings.Ui.Services;
using Exo.Utils;

namespace Exo.Overlay;

internal sealed class ExoUiPipeClientConnection : PipeClientConnection, IPipeClientConnection<ExoUiPipeClientConnection>
{
	private static readonly ImmutableArray<byte> GitCommitId = GitCommitHelper.GetCommitId(typeof(ExoUiPipeClientConnection).Assembly);

	public static ExoUiPipeClientConnection Create(PipeClient<ExoUiPipeClientConnection> client, NamedPipeClientStream stream)
	{
		var helperPipeClient = (ExoUiPipeClient)client;
		return new(client, stream, helperPipeClient.MenuChannel, helperPipeClient.SensorDeviceChannel);
	}

	private readonly ResettableChannel<MenuChangeNotification> _menuChannel;
	private readonly ResettableChannel<SensorDeviceInformation> _sensorDeviceChannel;
	private SensorWatchOperation?[]? _sensorWatchOperations;

	private ExoUiPipeClientConnection
	(
		PipeClient client,
		NamedPipeClientStream stream,
		ResettableChannel<MenuChangeNotification> menuChannel,
		ResettableChannel<SensorDeviceInformation> sensorDeviceChannel
	) : base(client, stream)
	{
		_menuChannel = menuChannel;
		_sensorDeviceChannel = sensorDeviceChannel;
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
				if (count == 0) return;
				// If the message processing does not indicate success, we can close the connection.
				if (!await ProcessMessageAsync(buffer.Span[..count]).ConfigureAwait(false)) return;
			}
		}
		finally
		{
			_menuChannel.Reset();
			_sensorDeviceChannel.Reset();
		}
	}

	protected override ValueTask OnDisposedAsync()
	{
		if (Interlocked.Exchange(ref _sensorWatchOperations, null) is { } sensorWatchOperations)
		{
			for (int i = 0; i < sensorWatchOperations.Length; i++)
			{
				if (Interlocked.Exchange(ref sensorWatchOperations[i], null) is { } sensorWatchOperation)
				{
					sensorWatchOperation.OnConnectionClosed();
				}
			}
		}
		return ValueTask.CompletedTask;
	}

	private ValueTask<bool> ProcessMessageAsync(ReadOnlySpan<byte> data) => ProcessMessageAsync((ExoUiProtocolServerMessage)data[0], data[1..]);

	private ValueTask<bool> ProcessMessageAsync(ExoUiProtocolServerMessage message, ReadOnlySpan<byte> data)
	{
		switch (message)
		{
		case ExoUiProtocolServerMessage.NoOp:
			goto Success;
		case ExoUiProtocolServerMessage.GitVersion:
			if (data.Length != 20) goto Failure;
#if DEBUG
			ConfirmVersion(data.ToImmutableArray());
#else
			if (GitCommitId.IsDefaultOrEmpty) ConfirmVersion(data.ToImmutableArray());
			else ConfirmVersion(GitCommitId);
#endif
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemEnumeration:
			ProcessCustomMenu(WatchNotificationKind.Enumeration, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemAdd:
			ProcessCustomMenu(WatchNotificationKind.Addition, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemRemove:
			ProcessCustomMenu(WatchNotificationKind.Removal, data);
			goto Success;
		case ExoUiProtocolServerMessage.CustomMenuItemUpdate:
			ProcessCustomMenu(WatchNotificationKind.Update, data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorDevice:
			ProcessSensorDevice(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorStart:
			ProcessSensorStart(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorValue:
			ProcessSensorValue(data);
			goto Success;
		case ExoUiProtocolServerMessage.SensorStop:
			return ProcessSensorStopAsync(data);
		}
	Success:;
		return new(true);
	Failure:;
		return new(false);
	}

	private async void ConfirmVersion(ImmutableArray<byte> version)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using var cts = CreateWriteCancellationTokenSource(default);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoUiProtocolClientMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private void ProcessCustomMenu(WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		var channelWriter = _menuChannel.CurrentWriter;
		var reader = new BufferReader(data);

		channelWriter.TryWrite
		(
			new()
			{
				Kind = kind,
				ParentItemId = reader.Read<Guid>(),
				Position = reader.Read<uint>(),
				ItemId = reader.Read<Guid>(),
				ItemType = (MenuItemType)reader.ReadByte(),
				Text = reader.RemainingLength > 0 ? reader.ReadVariableString() ?? "" : null
			}
		);
	}

	private void ProcessSensorDevice(ReadOnlySpan<byte> data)
	{
		var channelWriter = _sensorDeviceChannel.CurrentWriter;
		var reader = new BufferReader(data);

		var deviceId = reader.ReadGuid();
		var sensors = new SensorInformation[reader.ReadVariableUInt32()];

		for (int i = 0; i < sensors.Length; i++)
		{
			var sensorId = reader.ReadGuid();
			var dataType = (SensorDataType)reader.ReadByte();
			var capabilities = (SensorCapabilities)reader.ReadByte();
			string unit = reader.ReadVariableString() ?? "";
			VariantNumber minimumValue = (capabilities & SensorCapabilities.HasMinimumValue) != 0 ? Read(ref reader, dataType) : default;
			VariantNumber maximumValue = (capabilities & SensorCapabilities.HasMaximumValue) != 0 ? Read(ref reader, dataType) : default;
			sensors[i] = new()
			{
				SensorId = sensorId,
				DataType = dataType,
				Capabilities = capabilities,
				Unit = unit,
				ScaleMinimumValue = minimumValue,
				ScaleMaximumValue = maximumValue,
			};
		}

		channelWriter.TryWrite
		(
			new()
			{
				DeviceId = deviceId,
				Sensors = ImmutableCollectionsMarshal.AsImmutableArray(sensors),
			}
		);

		static VariantNumber Read(ref BufferReader reader, SensorDataType dataType)
		{
			switch (dataType)
			{
			case SensorDataType.UInt8: return reader.ReadByte();
			case SensorDataType.UInt16: return reader.Read<ushort>();
			case SensorDataType.UInt32: return reader.Read<uint>();
			case SensorDataType.UInt64: return reader.Read<ulong>();
			case SensorDataType.UInt128: return reader.Read<UInt128>();
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

	private void ProcessSensorStart(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		var status = (SensorStartStatus)reader.ReadByte();
		if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Volatile.Read(ref sensorWatchOperations[streamId]) is { } operation)
		{
			operation.OnStart(status);
		}
	}

	private void ProcessSensorValue(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Volatile.Read(ref sensorWatchOperations[streamId]) is { } operation)
		{
			operation.OnValue(data[(int)((uint)data.Length - (uint)reader.RemainingLength)..]);
		}
	}

	private ValueTask<bool> ProcessSensorStopAsync(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		uint streamId = reader.ReadVariableUInt32();
		return ProcessSensorStopAsync(streamId);
	}
	private async ValueTask<bool> ProcessSensorStopAsync(uint streamId)
	{
		try
		{
			// We need to acquire the lock because the array needs to be written to.
			using (await WriteLock.WaitAsync(GetDefaultWriteCancellationToken()).ConfigureAwait(false))
			{
				if (_sensorWatchOperations is { } sensorWatchOperations && streamId < (uint)sensorWatchOperations.Length && Interlocked.Exchange(ref _sensorWatchOperations[streamId], null) is { } operation)
				{
					operation.OnStop();
				}
			}
		}
		catch
		{
		}
		return true;
	}

	internal async ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		using var cts = CreateWriteCancellationTokenSource(cancellationToken);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..17];
			FillBuffer(buffer.Span, menuItemId);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, Guid menuItemId)
		{
			buffer[0] = (byte)ExoUiProtocolClientMessage.InvokeMenuCommand;
			Unsafe.WriteUnaligned(ref buffer[1], menuItemId);
		}
	}

	public async Task<SensorWatchOperation<TValue>> WatchSensorValuesAsync<TValue>(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
		where TValue : unmanaged, INumber<TValue>
	{
		Task<SensorWatchOperation<TValue>> startTask;
		cancellationToken.ThrowIfCancellationRequested();
		// Not sure if we can use the provided cancellation token to allow cancel writes at all, so for now, resort to the global write cancellation.
		// This shouldn't change much anyway, as pipe write operations should not block for a long time.
		var writeCancellationToken = GetDefaultWriteCancellationToken();
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeCancellationToken))
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			// In many cases, we won't have an excessive amount of sensors, so it is easier to just find any free slot in the (current) array by iterating over it.
			// This can be improved later on if this implementation turns out to be too slow.
			uint streamId = 0;
			var sensorWatchOperations = _sensorWatchOperations;
			if (sensorWatchOperations is null) Volatile.Write(ref _sensorWatchOperations, sensorWatchOperations = new SensorWatchOperation[8]);
			for (; streamId < sensorWatchOperations.Length; streamId++)
			{
				if (sensorWatchOperations[streamId] is null) break;
			}
			if (streamId >= sensorWatchOperations.Length)
			{
				Array.Resize(ref sensorWatchOperations, (int)(2 * (uint)sensorWatchOperations.Length));
				Volatile.Write(ref _sensorWatchOperations, sensorWatchOperations);
			}
			var operation = new SensorWatchOperation<TValue>(this, streamId);
			sensorWatchOperations[streamId] = operation;
			startTask = operation.WaitForStartAsync();
			if (cancellationToken.IsCancellationRequested)
			{
				sensorWatchOperations[streamId] = null;
				cancellationToken.ThrowIfCancellationRequested();
			}
			var buffer = WriteBuffer;
			WriteSensorStart(buffer.Span, streamId, deviceId, sensorId);
			// TODO: Find out if cancellation implies that bytes are not written.
			await WriteAsync(buffer, writeCancellationToken).ConfigureAwait(false);
		}
		return await startTask.ConfigureAwait(false);

		static void WriteSensorStart(Span<byte> buffer, uint streamId, Guid deviceId, Guid sensorId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.SensorStart);
			writer.WriteVariable(streamId);
			writer.Write(deviceId);
			writer.Write(sensorId);
		}
	}

	internal async ValueTask StopWatchingSensorValuesAsync(uint streamId)
	{
		var cancellationToken = GetDefaultWriteCancellationToken();
		try
		{
			using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var buffer = WriteBuffer;
				WriteSensorStop(buffer.Span, streamId);
				await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			throw new PipeClosedException();
		}

		static void WriteSensorStop(Span<byte> buffer, uint streamId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolClientMessage.SensorStop);
			writer.WriteVariable(streamId);
		}
	}
}

internal sealed class ExoUiPipeClient : PipeClient<ExoUiPipeClientConnection>, ISensorService
{
	internal ResettableChannel<MenuChangeNotification> MenuChannel { get; }
	internal ResettableChannel<SensorDeviceInformation> SensorDeviceChannel { get; }

	public ExoUiPipeClient
	(
		string pipeName,
		ResettableChannel<MenuChangeNotification> menuChannel,
		ResettableChannel<SensorDeviceInformation> sensorDeviceChannel
	) : base(pipeName, PipeTransmissionMode.Message)
	{
		MenuChannel = menuChannel;
		SensorDeviceChannel = sensorDeviceChannel;
	}

	public async ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		if (CurrentConnection is { } connection)
		{
			await connection.InvokeMenuItemAsync(menuItemId, cancellationToken);
		}
	}

	IAsyncEnumerable<TValue> ISensorService.WatchValuesAsync<TValue>(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
	{
		if (CurrentConnection is { } connection) return WatchSensorValuesAsync<TValue>(connection, deviceId, sensorId, cancellationToken);
		else return EmptyAsyncEnumerable<TValue>.Instance;
	}

	private static async IAsyncEnumerable<TValue> WatchSensorValuesAsync<TValue>(ExoUiPipeClientConnection connection, Guid deviceId, Guid sensorId, [EnumeratorCancellation] CancellationToken cancellationToken)
		where TValue : unmanaged, INumber<TValue>
	{
		var operation = await connection.WatchSensorValuesAsync<TValue>(deviceId, sensorId, cancellationToken).ConfigureAwait(false);
		var reader = operation.Reader;
		while (true)
		{
			try
			{
				await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				await operation.DisposeAsync();
				throw;
			}
			while (reader.TryRead(out var value))
			{
				yield return value;
			}
		}
	}
}

internal sealed class EmptyAsyncEnumerable<TValue> : IAsyncEnumerable<TValue>
{
	public static readonly EmptyAsyncEnumerable<TValue> Instance = new();

	private sealed class Enumerator : IAsyncEnumerator<TValue>
	{
		public static readonly Enumerator Instance = new();

		public ValueTask<bool> MoveNextAsync() => new(false);

		public TValue Current => default!;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default) => Enumerator.Instance;
}

internal abstract class SensorWatchOperation : IAsyncDisposable
{
	private protected static readonly UnboundedChannelOptions ChannelOptions = new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false };

	private const uint StateStarting = 0;
	private const uint StateStoppingAfterStart = 1;
	private const uint StateStarted = 2;
	private const uint StateStopping = 3;
	private const uint StateStopped = 4;

	private object _channelOrTaskCompletionSource;
	private readonly ExoUiPipeClientConnection _connection;
	private readonly uint _streamId;
	private uint _state;
	private TaskCompletionSource? _disposeTaskCompletionSource;

	private protected SensorWatchOperation(object channelOrTaskCompletionSource, ExoUiPipeClientConnection connection, uint streamId)
	{
		_channelOrTaskCompletionSource = channelOrTaskCompletionSource;
		_connection = connection;
		_streamId = streamId;
	}

	protected object ChannelOrTaskCompletionSource => _channelOrTaskCompletionSource;

	private protected abstract void CreateChannelAndNotifyStart(ref object channelOrTaskCompletionSource);
	private protected abstract void NotifyStartError(Exception ex);
	private protected abstract void NotifyStop(Exception? ex);

	internal abstract void OnValue(ReadOnlySpan<byte> data);

	internal void OnStart(SensorStartStatus status)
	{
		if (status == SensorStartStatus.Success)
		{
			uint state = Volatile.Read(ref _state);
			while (true)
			{
				if (state is StateStarting)
				{
					if (StateStarting == (state = Interlocked.CompareExchange(ref _state, StateStarted, StateStarting)))
					{
						CreateChannelAndNotifyStart(ref _channelOrTaskCompletionSource);
						break;
					}
				}
				else if (state is StateStoppingAfterStart)
				{
					// This does complete the stopping-after-start process in an asynchronous way.
					DisposeAfterStarted();
					break;
				}
			}
		}
		else
		{
			if (Interlocked.Exchange(ref _state, StateStopped) is StateStarting or StateStoppingAfterStart)
			{
				var ex = ExceptionDispatchInfo.SetCurrentStackTrace
				(
					status switch
					{
						SensorStartStatus.DeviceNotFound => new InvalidOperationException("Device not found."),
						SensorStartStatus.SensorNotFound => new InvalidOperationException("Sensor not found."),
						SensorStartStatus.StreamIdAlreadyInUse => new InvalidOperationException("Stream ID already in use."),
						_ => new InvalidOperationException("Could not start watching values."),
					}
				);
				NotifyStartError(ex);
				Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
			}
		}
	}

	internal void OnStop()
	{
		var state = Volatile.Read(ref _state);
		while (true)
		{
			if (state is StateStopped) return;
			if (state == (state = Interlocked.CompareExchange(ref _state, StateStopped, state)))
			{
				NotifyStop(null);
				Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
				break;
			}
		}
	}

	internal void OnConnectionClosed()
	{
		var state = Volatile.Read(ref _state);
		while (true)
		{
			if (state is StateStopped) return;

			if (state == (state = Interlocked.CompareExchange(ref _state, StateStopped, state)))
			{
				var exception = ExceptionDispatchInfo.SetCurrentStackTrace(new PipeClosedException());
				if (state is StateStarting or StateStoppingAfterStart)
				{
					NotifyStartError(exception);
				}
				else
				{
					NotifyStop(exception);
				}
				Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
				return;
			}
		}
	}

	public ValueTask DisposeAsync()
	{
		var state = Volatile.Read(ref _state);
		while (true)
		{
			if (state is StateStopped) return ValueTask.CompletedTask;
			if (state is StateStopping or StateStoppingAfterStart)
			{
				if (Volatile.Read(ref _disposeTaskCompletionSource) is { } tcs)
				{
					return new(tcs.Task);
				}
				state = Volatile.Read(ref _state);
				continue;
			}

			// Ensure that we have published a task completion source before cancelling anything.
			if (Volatile.Read(ref _disposeTaskCompletionSource) is null && Interlocked.CompareExchange(ref _disposeTaskCompletionSource, new(TaskCreationOptions.RunContinuationsAsynchronously), null) is null)
			{
				state = Volatile.Read(ref _state);
				// In the event where we switched to the final state in between, we should rollback the state that has been added.
				if (state is StateStopped)
				{
					Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
					return ValueTask.CompletedTask;
				}
			}

			if (state == (state = Interlocked.CompareExchange(ref _state, state is StateStarting ? StateStoppingAfterStart : StateStopping, state)))
			{
				if (state is StateStarting)
				{
					state = StateStoppingAfterStart;
				}
				else
				{
					return GracefullyDisposeAsync();
				}
			}
		}
	}

	private async void DisposeAfterStarted()
	{
		if (Interlocked.CompareExchange(ref _state, StateStopping, StateStoppingAfterStart) == StateStoppingAfterStart)
		{
			try
			{
				await GracefullyDisposeAsync().ConfigureAwait(false);
			}
			catch
			{
			}
		}
		else
		{
			Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
		}
		NotifyStartError(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(GetType().FullName)));
	}

	private async ValueTask GracefullyDisposeAsync()
	{
		try
		{
			await _connection.StopWatchingSensorValuesAsync(_streamId).ConfigureAwait(false);
		}
		catch (PipeClosedException)
		{
			Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
			return;
		}
		if (Volatile.Read(ref _disposeTaskCompletionSource) is { } tcs)
		{
			await tcs.Task.ConfigureAwait(false);
		}
	}
}

internal sealed class SensorWatchOperation<TValue> : SensorWatchOperation
	where TValue : unmanaged, INumber<TValue>
{
	public SensorWatchOperation(ExoUiPipeClientConnection connection, uint streamId)
		: base(new TaskCompletionSource<SensorWatchOperation>(TaskCreationOptions.RunContinuationsAsynchronously), connection, streamId)
	{
	}

	public ChannelReader<TValue> Reader => Unsafe.As<Channel<TValue>>(ChannelOrTaskCompletionSource).Reader;

	private protected override void CreateChannelAndNotifyStart(ref object channelOrTaskCompletionSource)
	{
		var tcs = Unsafe.As<TaskCompletionSource<SensorWatchOperation<TValue>>>(channelOrTaskCompletionSource);
		channelOrTaskCompletionSource = Channel.CreateUnbounded<TValue>(ChannelOptions);
		tcs.TrySetResult(this);
	}

	private protected override void NotifyStartError(Exception ex) => Unsafe.As<TaskCompletionSource<SensorWatchOperation<TValue>>>(ChannelOrTaskCompletionSource).TrySetException(ex);
	private protected override void NotifyStop(Exception? ex) => Unsafe.As<Channel<TValue>>(ChannelOrTaskCompletionSource).Writer.Complete(ex);
	internal Task<SensorWatchOperation<TValue>> WaitForStartAsync() => Unsafe.As<TaskCompletionSource<SensorWatchOperation<TValue>>>(ChannelOrTaskCompletionSource).Task;

	internal override void OnValue(ReadOnlySpan<byte> data)
	{
		if ((uint)data.Length < Unsafe.SizeOf<TValue>()) throw new InvalidDataException();
		Unsafe.As<Channel<TValue>>(ChannelOrTaskCompletionSource).Writer.TryWrite(Unsafe.ReadUnaligned<TValue>(in data[0]));
	}
}
