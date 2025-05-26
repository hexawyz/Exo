using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Exo.Primitives;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal sealed partial class SensorService
{
	// For sensor watching, we'll use bounded channels that drop the oldest datapoints, so that the service can avoid accumulating excessive amounts of data if a watcher is stuck.
	private static readonly BoundedChannelOptions SensorWatchChannelOptions = new BoundedChannelOptions(10)
	{
		AllowSynchronousContinuations = false,
		SingleReader = true,
		SingleWriter = true,
		FullMode = BoundedChannelFullMode.DropOldest,
	};

	private interface IPolledSensorState
	{
		//ValueTask PollAsync(CancellationToken cancellationToken);
	}

	private interface IGroupedPolledSensorState
	{
		IPolledSensor Sensor { get; }
		GroupedPolledSensorPendingOperation PendingOperation { get; set; }
		void RefreshDataPoint(DateTime dateTime);
	}

	private enum GroupedPolledSensorPendingOperation : byte
	{
		None = 0,
		EnableDisabled = 1,
		DisableEnabled = 2,
		DisableNotEnabled = 1,
	}

	private abstract class SensorState : IAsyncDisposable
	{
		public static SensorState Create(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor sensor)
			=> TypeToSensorDataTypeMapping[sensor.ValueType] switch
			{
				SensorDataType.UInt8 => Create<byte>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.UInt16 => Create<ushort>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.UInt32 => Create<uint>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.UInt64 => Create<ulong>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.UInt128 => Create<UInt128>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.SInt8 => Create<sbyte>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.SInt16 => Create<short>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.SInt32 => Create<int>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.SInt64 => Create<long>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.SInt128 => Create<Int128>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.Float16 => Create<Half>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.Float32 => Create<float>(logger, sensorService, groupedQueryState, sensor),
				SensorDataType.Float64 => Create<double>(logger, sensorService, groupedQueryState, sensor),
				_ => throw new InvalidOperationException()
			};

		public static SensorState<TValue> Create<TValue>(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor sensor)
			where TValue : struct, INumber<TValue>
			=> Create(logger, sensorService, groupedQueryState, (ISensor<TValue>)sensor);

		public static SensorState<TValue> Create<TValue>(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
			=> sensor.Kind switch
			{
				SensorKind.Internal => new InternalSensorState<TValue>(logger, (IInternalSensor<TValue>)sensor),
				SensorKind.Polled => CreatePolledSensorState(logger, sensorService, groupedQueryState, (IPolledSensor<TValue>)sensor),
				SensorKind.Streamed => new StreamedSensorState<TValue>(logger, (IStreamedSensor<TValue>)sensor),
				_ => throw new InvalidOperationException(),
			};

		private static SensorState<TValue> CreatePolledSensorState<TValue>(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, IPolledSensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
			=> sensor.GroupedQueryMode != GroupedQueryMode.None ?
				CreateGroupedPolledSensorState(logger, sensorService, groupedQueryState, sensor) :
				new PolledSensorState<TValue>(logger, sensorService, sensor);

		private static GroupedPolledSensorState<TValue> CreateGroupedPolledSensorState<TValue>(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, IPolledSensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
		{
			ArgumentNullException.ThrowIfNull(groupedQueryState);
			return new GroupedPolledSensorState<TValue>(logger, sensorService, groupedQueryState, sensor);
		}

		private readonly ISensor _sensor;
		private TaskCompletionSource _watchSignal;
		private CancellationTokenSource? _watchCancellationTokenSource;
		private readonly ILogger<SensorState> _logger;
		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _watchAsyncTask;

		public ISensor Sensor => _sensor;

		protected SensorState(ILogger<SensorState> logger, ISensor sensor)
		{
			_sensor = sensor;
			_watchSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
			_cancellationTokenSource = new();
			_logger = logger;
			_watchAsyncTask = RunAsync(_cancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				_watchSignal.TrySetCanceled(cts.Token);
				await _watchAsyncTask.ConfigureAwait(false);
				cts.Dispose();
			}
		}

		protected bool IsDisposed => Volatile.Read(ref _cancellationTokenSource) is null;

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await _watchSignal.Task.ConfigureAwait(false);
					if (cancellationToken.IsCancellationRequested) return;
					var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					var watchCancellationToken = cts.Token;
					Volatile.Write(ref _watchCancellationTokenSource, cts);
					try
					{
						await WatchValuesAsync(watchCancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (watchCancellationToken.IsCancellationRequested)
					{
					}
					catch (Exception ex)
					{
						_logger.SensorServiceSensorStateWatchError(ex);
					}
					finally
					{
						ClearAndDisposeCancellationTokenSource(ref _watchCancellationTokenSource);
					}
					if (cancellationToken.IsCancellationRequested) return;
					_watchSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			finally
			{
				OnStateCompletion();
			}
		}

		protected void StartWatching() => _watchSignal.TrySetResult();

		protected void StopWatching() => ClearAndDisposeCancellationTokenSource(ref _watchCancellationTokenSource);

		// This method will be called once at the very end of the lifetime in order to propagate completion to the watchers and allow them to end.
		// Because it is called at the end, we can be sure that any potential channel write done in this method will be isolated and will not conflict with the single-writer policy.
		protected abstract void OnStateCompletion();

		// This method is the inner loop for watching device, and provides the specialized watching mechanism for different sensor types.
		// It is executed as part of RunAsync, and its execution will be controlled by matched calls to StartWatching and StopWatching, as well as DisposeAsync.
		// Once the instance is disposed, this method can never execute.
		// Execution of this method will always be completed before CleanupValueListeners is called.
		protected abstract ValueTask WatchValuesAsync(CancellationToken cancellationToken);

		public SensorDataType DataType => TypeToSensorDataTypeMapping[_sensor.ValueType];
	}

	private abstract class SensorState<TValue> : SensorState, IChangeSource<SensorDataPoint<TValue>>
		where TValue : struct, INumber<TValue>
	{
		public new ISensor<TValue> Sensor => Unsafe.As<ISensor<TValue>>(base.Sensor);

		private ChangeBroadcaster<SensorDataPoint<TValue>> _valueBroadcaster;

		protected SensorState(ILogger<SensorState> logger, ISensor<TValue> sensor) : base(logger, sensor)
		{
		}

		protected void OnDataPointReceived(TValue value) => OnDataPointReceived(DateTime.UtcNow, value);

		protected void OnDataPointReceived(DateTime dateTime, TValue value) => OnDataPointReceived(new SensorDataPoint<TValue>(dateTime, value));

		protected void OnDataPointReceived(SensorDataPoint<TValue> dataPoint) => _valueBroadcaster.Push(dataPoint);

		ValueTask<SensorDataPoint<TValue>[]?> IChangeSource<SensorDataPoint<TValue>>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<SensorDataPoint<TValue>> writer, CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (_valueBroadcaster.Register(writer))
				{
					StartWatching();
				}
			}
			return new([]);
		}

		void IChangeSource<SensorDataPoint<TValue>>.UnsafeUnregisterWatcher(ChannelWriter<SensorDataPoint<TValue>> writer)
		{
			lock (this)
			{
				if (_valueBroadcaster.Unregister(writer))
				{
					StopWatching();
				}
				writer.TryComplete();
			}
		}

		ValueTask IChangeSource<SensorDataPoint<TValue>>.SafeUnregisterWatcherAsync(ChannelWriter<SensorDataPoint<TValue>> writer)
		{
			lock (this)
			{
				if (_valueBroadcaster.Unregister(writer))
				{
					StopWatching();
				}
				writer.TryComplete();
			}
			return ValueTask.CompletedTask;
		}

		// NB: This method must be exclusive with the OnDataPointReceived methods.
		protected sealed override void OnStateCompletion()
		{
			// TODO: Delegate the error creation to the TryComplete method. (Generic with new() constraint ?)
			var valueBroadcaster = _valueBroadcaster.GetSnapshot();
			if (!valueBroadcaster.IsEmpty)
			{
				_valueBroadcaster.TryComplete(ExceptionDispatchInfo.SetCurrentStackTrace(new DeviceDisconnectedException()));
			}
		}
	}

	private sealed class InternalSensorState<TValue> : SensorState<TValue>
		where TValue : struct, INumber<TValue>
	{
		public InternalSensorState(ILogger<SensorState> logger, IInternalSensor<TValue> sensor) : base(logger, sensor)
		{
		}

		protected override ValueTask WatchValuesAsync(CancellationToken cancellationToken)
			=> ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("This sensor can not be watched.")));
	}

	private sealed class PolledSensorState<TValue> : SensorState<TValue>, IPolledSensorState
		where TValue : struct, INumber<TValue>
	{
		public new IPolledSensor<TValue> Sensor => Unsafe.As<IPolledSensor<TValue>>(base.Sensor);

		private readonly SensorService _sensorService;

		public PolledSensorState(ILogger<SensorState> logger, SensorService sensorService, IPolledSensor<TValue> sensor) : base(logger, sensor)
		{
			_sensorService = sensorService;
		}

		protected override async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var scheduler = _sensorService._pollingScheduler;
			using (var tick = scheduler.StartTicking())
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await tick.WaitAsync().ConfigureAwait(false) || cancellationToken.IsCancellationRequested) return;
					OnDataPointReceived(await Sensor.GetValueAsync(cancellationToken).ConfigureAwait(false));
				}
			}
		}
	}

	private sealed class GroupedPolledSensorState<TValue> : SensorState<TValue>, IGroupedPolledSensorState
		where TValue : struct, INumber<TValue>
	{
		public new IPolledSensor<TValue> Sensor => Unsafe.As<IPolledSensor<TValue>>(base.Sensor);
		IPolledSensor IGroupedPolledSensorState.Sensor => Sensor;

		private readonly SensorService _sensorService;
		private readonly GroupedQueryState _groupedQueryState;
		private GroupedPolledSensorPendingOperation _pendingOperation;

		public GroupedPolledSensorState(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState groupedQueryState, IPolledSensor<TValue> sensor)
			: base(logger, sensor)
		{
			_sensorService = sensorService;
			_groupedQueryState = groupedQueryState;
		}

		protected override ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return ValueTask.CompletedTask;
			// Sensors managed by grouped queries are polled from the GroupedQueryState.
			// The code here is just setting things up for enabling the sensor to be refreshed by the grouped query.
			var tcs = new TaskCompletionSource(this, TaskCreationOptions.RunContinuationsAsynchronously);
			_groupedQueryState.Acquire(this);
			// Because we exclusively depend on the cancellation token being cancelled, I don't believe it is necessary to do anything with the registration here.
			// Logically, cancellation should clear out the internal cancellation registrations and prevent registering new ones (calling the callback immediately)
			_= cancellationToken.UnsafeRegister
			(
				static state =>
				{
					var tcs = (TaskCompletionSource)state!;
					var @this = (GroupedPolledSensorState<TValue>)tcs.Task.AsyncState!;
					@this._groupedQueryState.Release(@this);
					tcs.TrySetResult();
				},
				tcs
			);
			return new(tcs.Task);
		}

		public void RefreshDataPoint(DateTime dateTime)
		{
			if (Sensor.GroupedQueryMode == GroupedQueryMode.Enabled && Sensor.TryGetLastValue(out var value))
			{
				OnDataPointReceived(dateTime, value);
			}
		}

		public GroupedPolledSensorPendingOperation PendingOperation { get => _pendingOperation; set => _pendingOperation = value; }
	}

	private sealed class StreamedSensorState<TValue> : SensorState<TValue>
		where TValue : struct, INumber<TValue>
	{
		public new IStreamedSensor<TValue> Sensor => Unsafe.As<IStreamedSensor<TValue>>(base.Sensor);

		public StreamedSensorState(ILogger<SensorState> logger, IStreamedSensor<TValue> sensor) : base(logger, sensor)
		{
		}

		protected override async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			await foreach (var dataPoint in Sensor.EnumerateValuesAsync(cancellationToken).ConfigureAwait(false))
			{
				OnDataPointReceived(dataPoint);
			}
		}
	}
}
