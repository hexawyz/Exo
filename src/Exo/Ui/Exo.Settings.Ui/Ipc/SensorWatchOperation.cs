using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Exo.Ipc;

namespace Exo.Settings.Ui.Ipc;

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
				if (state is StateStarting)
					if (StateStarting == (state = Interlocked.CompareExchange(ref _state, StateStarted, StateStarting)))
					{
						CreateChannelAndNotifyStart(ref _channelOrTaskCompletionSource);
						break;
					}
				else if (state is StateStoppingAfterStart)
				{
					// This does complete the stopping-after-start process in an asynchronous way.
					DisposeAfterStarted();
					break;
				}
		}
		else
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
					NotifyStartError(exception);
				else
					NotifyStop(exception);
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
					return new(tcs.Task);
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
				if (state is StateStarting)
					state = StateStoppingAfterStart;
				else
					return GracefullyDisposeAsync();
		}
	}

	private async void DisposeAfterStarted()
	{
		if (Interlocked.CompareExchange(ref _state, StateStopping, StateStoppingAfterStart) == StateStoppingAfterStart)
			try
			{
				await GracefullyDisposeAsync().ConfigureAwait(false);
			}
			catch
			{
			}
		else
			Interlocked.Exchange(ref _disposeTaskCompletionSource, null)?.TrySetResult();
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
			await tcs.Task.ConfigureAwait(false);
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
