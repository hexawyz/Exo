using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public sealed partial class SensorService
{
	// For sensor watching, we'll use bounded channels that drop the oldest datapoints, so that the service can avoid accumulating excessive amounts of data if a watcher is stuck.
	private static readonly BoundedChannelOptions SensorWatchChannelOptions = new BoundedChannelOptions(10)
	{
		AllowSynchronousContinuations = false,
		SingleReader = true,
		SingleWriter = false,
		FullMode = BoundedChannelFullMode.DropOldest,
	};

	private interface IPolledSensorState
	{
		//ValueTask PollAsync(CancellationToken cancellationToken);
	}

	private interface IGroupedPolledSensorState
	{
		public IPolledSensor Sensor { get; }
		void RefreshDataPoint(DateTime dateTime);
	}

	private abstract class SensorState : IAsyncDisposable
	{
		public static SensorState Create(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor sensor)
			=> SensorDataTypes[sensor.ValueType] switch
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
			=> sensor.IsPolled ?
				CreatePolledSensorState(logger, sensorService, groupedQueryState, (IPolledSensor<TValue>)sensor) :
				new StreamedSensorState<TValue>(logger, (IStreamedSensor<TValue>)sensor);

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
				await _watchAsyncTask.ConfigureAwait(false);
				cts.Dispose();
			}
		}

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
					ClearAndDisposeCancellationTokenSource(ref _watchCancellationTokenSource);
					if (cancellationToken.IsCancellationRequested) return;
					_watchSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		protected void StartWatching() => _watchSignal.TrySetResult();

		protected void StopWatching() => ClearAndDisposeCancellationTokenSource(ref _watchCancellationTokenSource);

		protected abstract ValueTask WatchValuesAsync(CancellationToken cancellationToken);
	}

	private abstract class SensorState<TValue> : SensorState
		where TValue : struct, INumber<TValue>
	{
		public new ISensor<TValue> Sensor => Unsafe.As<ISensor<TValue>>(base.Sensor);

		private ChannelWriter<SensorDataPoint<TValue>>[]? _valueListeners;

		protected SensorState(ILogger<SensorState> logger, ISensor<TValue> sensor) : base(logger, sensor)
		{
		}

		protected void OnDataPointReceived(TValue value) => OnDataPointReceived(DateTime.UtcNow, value);

		protected void OnDataPointReceived(DateTime dateTime, TValue value) => OnDataPointReceived(new SensorDataPoint<TValue>(dateTime, value));

		protected void OnDataPointReceived(SensorDataPoint<TValue> dataPoint) => Volatile.Read(ref _valueListeners).TryWrite(dataPoint);

		private void AddListener(ChannelWriter<SensorDataPoint<TValue>> listener)
		{
			// NB: This can possibly made lock-lighter, but the value of this change would have is uncertain.
			lock (this)
			{
				var listeners = _valueListeners.Add(listener);
				Volatile.Write(ref _valueListeners, listeners);
				if (listeners.Length == 1)
				{
					StartWatching();
				}
			}
		}

		private void RemoveListener(ChannelWriter<SensorDataPoint<TValue>> listener)
		{
			// NB: This can possibly made lock-lighter, but the value of this change would have is uncertain.
			lock (this)
			{
				var listeners = _valueListeners.Remove(listener);
				Volatile.Write(ref _valueListeners, listeners);
				if (listeners is null || listeners.Length == 0)
				{
					StopWatching();
				}
			}
		}

		public async IAsyncEnumerable<SensorDataPoint<TValue>> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var channel = Channel.CreateBounded<SensorDataPoint<TValue>>(SensorWatchChannelOptions);
			AddListener(channel);
			try
			{
				await foreach (var dataPoint in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
				{
					yield return dataPoint;
				}
			}
			finally
			{
				RemoveListener(channel);
			}
		}
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
			scheduler.Acquire();
			try
			{
				while (true)
				{
					await scheduler.WaitAsync(cancellationToken).ConfigureAwait(false);
					OnDataPointReceived(await Sensor.GetValueAsync(cancellationToken).ConfigureAwait(false));
				}
			}
			finally
			{
				scheduler.Release();
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

		public GroupedPolledSensorState(ILogger<SensorState> logger, SensorService sensorService, GroupedQueryState groupedQueryState, IPolledSensor<TValue> sensor)
			: base(logger, sensor)
		{
			_sensorService = sensorService;
			_groupedQueryState = groupedQueryState;
		}

		protected override async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			// Sensors managed by grouped queries are polled from the GroupedQueryState.
			// The code here is just setting things up for enabling the sensor to be refreshed by the grouped query.
			var tcs = new TaskCompletionSource();
			using (cancellationToken.Register(state => ((TaskCompletionSource)state!).TrySetResult(), tcs, false))
			{
				_groupedQueryState.Acquire(this);
				await tcs.Task.ConfigureAwait(false);
				_groupedQueryState.Release(this);
			}
			cancellationToken.ThrowIfCancellationRequested();
		}

		public void RefreshDataPoint(DateTime dateTime)
		{
			if (Sensor.GroupedQueryMode == GroupedQueryMode.Enabled && Sensor.TryGetLastValue(out var value))
			{
				OnDataPointReceived(dateTime, value);
			}
		}
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
