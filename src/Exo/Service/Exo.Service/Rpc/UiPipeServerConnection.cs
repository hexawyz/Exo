using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Rpc;
using Exo.Sensors;

namespace Exo.Service.Rpc;

internal sealed class UiPipeServerConnection : PipeServerConnection, IPipeServerConnection<UiPipeServerConnection>
{
	public static UiPipeServerConnection Create(PipeServer<UiPipeServerConnection> server, NamedPipeServerStream stream)
	{
		var uiPipeServer = (UiPipeServer)server;
		return new(server, stream, uiPipeServer.CustomMenuService, uiPipeServer.SensorService);
	}

	private readonly CustomMenuService _customMenuService;
	private readonly SensorService _sensorService;
	private int _state;
	private readonly Dictionary<uint, SensorWatchState> _sensorWatchStates;

	private UiPipeServerConnection
	(
		PipeServer server,
		NamedPipeServerStream stream,
		CustomMenuService customMenuService,
		SensorService sensorService
	) : base(server, stream)
	{
		_customMenuService = customMenuService;
		_sensorService = sensorService;
		_sensorWatchStates = new();
	}

	protected override ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

	private async Task WatchEventsAsync(CancellationToken cancellationToken)
	{
		var customMenuWatchTask = WatchCustomMenuChangesAsync(cancellationToken);

		await Task.WhenAll(customMenuWatchTask).ConfigureAwait(false);
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

		protected static nuint WriteStop(Span<byte> buffer, uint streamId)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.SensorStop);
			writer.WriteVariable(streamId);
			return writer.Length;
		}
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
			_task = WatchValuesAsync(enumerable, CancellationToken);
		}

		protected override ValueTask DisposeOnceAsync(CancellationToken canceledToken)
		{
			Interlocked.Exchange(ref _taskCompletionSource, null)?.TrySetCanceled(canceledToken);
			return new(_task);
		}

		public override void Start() => Interlocked.Exchange(ref _taskCompletionSource, null)?.TrySetResult();

		private async Task WatchValuesAsync(IAsyncEnumerable<SensorDataPoint<TValue>> enumerable, CancellationToken cancellationToken)
		{
			await _taskCompletionSource!.Task.ConfigureAwait(false);
			try
			{
				try
				{
					await foreach (var value in enumerable.ConfigureAwait(false))
					{
						using (await Connection.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							var buffer = Connection.WriteBuffer;
							nuint length = Write(buffer.Span, StreamId, value.Value);
							await Connection.WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);
						}
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					throw;
				}
				// We want to notify the client of the stream end.
				// Calling this helper method will be the simplest way for now, as it avoids dragging along the connection's cancellation token.
				if (Connection.TryGetDefaultWriteCancellationToken(out var writeCancellationToken))
				{
					using (await Connection.WriteLock.WaitAsync(writeCancellationToken).ConfigureAwait(false))
					{
						var buffer = Connection.WriteBuffer;
						nuint length = WriteStop(buffer.Span, StreamId);
						await Connection.WriteAsync(buffer[..(int)length], cancellationToken).ConfigureAwait(false);
						// We only need to remove the state from the global state if the connection is still open, which will be the case only if this code is reached.
						// In other cases, the connection will be trashed, so no need for complex handling.
						Connection._sensorWatchStates.Remove(StreamId);
					}
				}
			}
			catch (OperationCanceledException)
			{
			}

			static nuint Write(Span<byte> buffer, uint streamId, TValue value)
			{
				var writer = new BufferWriter(buffer);
				writer.Write((byte)ExoUiProtocolServerMessage.SensorValue);
				writer.WriteVariable(streamId);
				if (typeof(TValue) == typeof(byte))
				{
					writer.Write(Unsafe.BitCast<TValue, byte>(value));
				}
				else
				{
					writer.Write(value);
				}
				return writer.Length;
			}
		}
	}
}
