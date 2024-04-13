using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Sensors;

namespace Exo.Service;

public sealed partial class SensorService
{
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
		public static SensorState Create(SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor sensor)
			=> SensorDataTypes[sensor.ValueType] switch
			{
				SensorDataType.UInt8 => Create<byte>(sensorService, groupedQueryState, sensor),
				SensorDataType.UInt16 => Create<ushort>(sensorService, groupedQueryState, sensor),
				SensorDataType.UInt32 => Create<uint>(sensorService, groupedQueryState, sensor),
				SensorDataType.UInt64 => Create<ulong>(sensorService, groupedQueryState, sensor),
				SensorDataType.UInt128 => Create<UInt128>(sensorService, groupedQueryState, sensor),
				SensorDataType.SInt8 => Create<sbyte>(sensorService, groupedQueryState, sensor),
				SensorDataType.SInt16 => Create<short>(sensorService, groupedQueryState, sensor),
				SensorDataType.SInt32 => Create<int>(sensorService, groupedQueryState, sensor),
				SensorDataType.SInt64 => Create<long>(sensorService, groupedQueryState, sensor),
				SensorDataType.SInt128 => Create<Int128>(sensorService, groupedQueryState, sensor),
				SensorDataType.Float16 => Create<Half>(sensorService, groupedQueryState, sensor),
				SensorDataType.Float32 => Create<float>(sensorService, groupedQueryState, sensor),
				SensorDataType.Float64 => Create<double>(sensorService, groupedQueryState, sensor),
				_ => throw new InvalidOperationException()
			};

		public static SensorState<TValue> Create<TValue>(SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor sensor)
			where TValue : struct, INumber<TValue>
			=> Create(sensorService, groupedQueryState, (ISensor<TValue>)sensor);

		public static SensorState<TValue> Create<TValue>(SensorService sensorService, GroupedQueryState? groupedQueryState, ISensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
			=> sensor.IsPolled ?
				CreatePolledSensorState(sensorService, groupedQueryState, (IPolledSensor<TValue>)sensor) :
				new StreamedSensorState<TValue>((IStreamedSensor<TValue>)sensor);

		private static SensorState<TValue> CreatePolledSensorState<TValue>(SensorService sensorService, GroupedQueryState? groupedQueryState, IPolledSensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
			=> sensor.GroupedQueryMode != GroupedQueryMode.None ?
				CreateGroupedPolledSensorState(sensorService, groupedQueryState, sensor) :
				new PolledSensorState<TValue>(sensorService, sensor);

		private static GroupedPolledSensorState<TValue> CreateGroupedPolledSensorState<TValue>(SensorService sensorService, GroupedQueryState? groupedQueryState, IPolledSensor<TValue> sensor)
			where TValue : struct, INumber<TValue>
		{
			ArgumentNullException.ThrowIfNull(groupedQueryState);
			return new GroupedPolledSensorState<TValue>(sensorService, groupedQueryState, sensor);
		}

		private readonly ISensor _sensor;
		private TaskCompletionSource _watchSignal;
		private CancellationTokenSource? _watchCancellationTokenSource;
		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _watchAsyncTask;

		public ISensor Sensor => _sensor;

		protected SensorState(ISensor sensor)
		{
			_sensor = sensor;
			_watchSignal = new();
			_cancellationTokenSource = new();
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
						await WatchValuesAsync(watchCancellationToken);
					}
					catch (OperationCanceledException) when (watchCancellationToken.IsCancellationRequested)
					{
					}
					catch (Exception ex)
					{
						// TODO: Log.
					}
					if (cancellationToken.IsCancellationRequested) return;
					cts = Interlocked.Exchange(ref _watchCancellationTokenSource, null);
					cts?.Dispose();
					_watchSignal = new();
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}

		protected void StartWatching() => _watchSignal.TrySetResult();

		protected void StopWatching()
		{
			if (Interlocked.Exchange(ref _watchCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				cts.Dispose();
			}
		}

		protected abstract ValueTask WatchValuesAsync(CancellationToken cancellationToken);
	}

	private abstract class SensorState<TValue> : SensorState
		where TValue : struct, INumber<TValue>
	{
		public new ISensor<TValue> Sensor => Unsafe.As<ISensor<TValue>>(base.Sensor);

		private ChannelWriter<SensorDataPoint<TValue>>[]? _valueListeners;

		protected SensorState(ISensor<TValue> sensor) : base(sensor)
		{
		}

		protected void OnDataPointReceived(TValue value) => OnDataPointReceived(DateTime.UtcNow, value);

		protected void OnDataPointReceived(DateTime dateTime, TValue value)
			=> Volatile.Read(ref _valueListeners).TryWrite(new(dateTime, value));

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
			var channel = Watcher.CreateSingleWriterChannel<SensorDataPoint<TValue>>();
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

		public PolledSensorState(SensorService sensorService, IPolledSensor<TValue> sensor) : base(sensor)
		{
			_sensorService = sensorService;
		}

		//public async ValueTask PollAsync(CancellationToken cancellationToken)
		//{
		//	OnDataPointReceived(await Sensor.GetValueAsync(cancellationToken));
		//}

		protected override ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			// TODO: Non-grouped sensors. (Would need to have a per-device state querying the sensors in order maybe? Or we assume that non-grouped queries can run in parallel and leave synchro to the driver… Which might actually be easier)
			throw new NotSupportedException("Polled sensors that cannot be queried in a group are not supported yet.");
		}
	}

	private sealed class GroupedPolledSensorState<TValue> : SensorState<TValue>, IGroupedPolledSensorState
		where TValue : struct, INumber<TValue>
	{
		public new IPolledSensor<TValue> Sensor => Unsafe.As<IPolledSensor<TValue>>(base.Sensor);
		IPolledSensor IGroupedPolledSensorState.Sensor => Sensor;

		private readonly SensorService _sensorService;
		private readonly GroupedQueryState _groupedQueryState;

		public GroupedPolledSensorState(SensorService sensorService, GroupedQueryState groupedQueryState, IPolledSensor<TValue> sensor) : base(sensor)
		{
			_sensorService = sensorService;
			_groupedQueryState = groupedQueryState;
		}

		//public async ValueTask PollAsync(CancellationToken cancellationToken)
		//{
		//	OnDataPointReceived(await Sensor.GetValueAsync(cancellationToken));
		//}

		protected override async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			// Sensors managed by grouped queries are polled from the GroupedQueryState.
			// The code here is just setting things up for enabling the sensor t
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

		public StreamedSensorState(IStreamedSensor<TValue> sensor) : base(sensor)
		{
		}

		protected override async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			await foreach (var value in Sensor.EnumerateValuesAsync(cancellationToken).ConfigureAwait(false))
			{
				OnDataPointReceived(value);
			}
		}
	}
}
