using Exo.Features;

namespace Exo.Service;

public sealed partial class SensorService
{
	// Manages the grouped polling of sensors for a device, based on the scheduler tick.
	private sealed class GroupedQueryState : IAsyncDisposable
	{
		private readonly SensorService _sensorService;
		private readonly ISensorsGroupedQueryFeature _groupedQueryFeature;
		private readonly IGroupedPolledSensorState?[] _activeSensorStates;
		private int _referenceCount;
		private readonly object _lock;
		private TaskCompletionSource _enableSignal;
		private CancellationTokenSource? _disableCancellationTokenSource;
		private CancellationTokenSource? _disposeCancellationTokenSource;
		private readonly Task _runTask;

		public GroupedQueryState(SensorService sensorService, ISensorsGroupedQueryFeature groupedQueryFeature, int capacity)
		{
			_sensorService = sensorService;
			_groupedQueryFeature = groupedQueryFeature;
			_activeSensorStates = new IGroupedPolledSensorState?[capacity];
			_lock = new();
			_enableSignal = new();
			_disposeCancellationTokenSource = new();
			_runTask = RunAsync(_disposeCancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposeCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				Volatile.Read(ref _enableSignal).TrySetResult();
				await _runTask.ConfigureAwait(false);
				cts.Dispose();
			}
		}

		// This function is called by a sensor state to setup grouped querying for it.
		// NB: The sensor state *WILL* ensure that this method is never called twice in succession for a given sensor.
		public void Acquire(IGroupedPolledSensorState state)
		{
			_groupedQueryFeature.AddSensor(state.Sensor);
			lock (_lock)
			{
				_activeSensorStates[_referenceCount] = state;
				if (_referenceCount++ == 0)
				{
					_sensorService._pollingScheduler.Acquire();
					_enableSignal.TrySetResult();
				}
			}
		}

		// This function is called by a sensor state to cancel grouped querying for it.
		// NB: The sensor state *WILL* ensure that this method is never called twice in succession for a given sensor.
		public void Release(IGroupedPolledSensorState state)
		{
			_groupedQueryFeature.RemoveSensor(state.Sensor);
			lock (_lock)
			{
				int index = Array.IndexOf(_activeSensorStates, state, 0, _referenceCount);
				--_referenceCount;
				if ((uint)index < (uint)_referenceCount)
				{
					Array.Copy(_activeSensorStates, index + 1, _activeSensorStates, index, _referenceCount - index);
				}
				else
				{
					_activeSensorStates[_referenceCount] = null;
				}
				if (_referenceCount == 0)
				{
					_sensorService._pollingScheduler.Release();
					if (Interlocked.Exchange(ref _disableCancellationTokenSource, null) is { } cts)
					{
						cts.Cancel();
						cts.Dispose();
					}
				}
			}
		}

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await _enableSignal.Task.ConfigureAwait(false);
					if (cancellationToken.IsCancellationRequested) return;
					var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					var queryCancellationToken = cts.Token;
					Volatile.Write(ref _disableCancellationTokenSource, cts);
					try
					{
						await QueryValuesAsync(queryCancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (queryCancellationToken.IsCancellationRequested)
					{
					}
					if (cancellationToken.IsCancellationRequested) return;
					cts = Interlocked.Exchange(ref _disableCancellationTokenSource, null);
					cts?.Dispose();
					Volatile.Write(ref _enableSignal, new());
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				// TODO: Log
			}
		}

		private async ValueTask QueryValuesAsync(CancellationToken cancellationToken)
		{
			while (true)
			{
				var now = DateTime.UtcNow;
				foreach (var state in _activeSensorStates)
				{
					// If we encounter a null value, this is the end of the list.
					// We could use the _referenceCount, but it would not avoid the need for a null check (race conditions), and would require bounds-checking as well as a volatile read on every loop.
					if (state is null) break;
					state.RefreshDataPoint(now);
				}
				await _groupedQueryFeature.QueryValuesAsync(cancellationToken).ConfigureAwait(false);
				await _sensorService._pollingScheduler.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
