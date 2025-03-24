using Exo.Features.Sensors;

namespace Exo.Service;

internal sealed partial class SensorService
{
	// Manages the grouped polling of sensors for a device, based on the scheduler tick.
	private sealed class GroupedQueryState : IAsyncDisposable
	{
		private readonly SensorService _sensorService;
		private readonly ISensorsGroupedQueryFeature _groupedQueryFeature;
		private readonly IGroupedPolledSensorState?[] _activeSensorStates;
		private int _referenceCount;
		private readonly Lock _lock;
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
			_enableSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
		// NB: This method reserves a slot for the new state and starts periodic polling if necessary, but the actual enabling of the sensor will be done in the polling loop.
		public void Acquire(IGroupedPolledSensorState state)
		{
			lock (_lock)
			{
				// If the same sensor is disabled then enabled in a quick succession, we can just revert the status to how it was previously.
				if (state.PendingOperation is GroupedPolledSensorPendingOperation.DisableEnabled)
				{
					state.PendingOperation = GroupedPolledSensorPendingOperation.None;
					return;
				}
				else if (state.PendingOperation is GroupedPolledSensorPendingOperation.DisableNotEnabled)
				{
					state.PendingOperation = GroupedPolledSensorPendingOperation.EnableDisabled;
					return;
				}

				int index = _referenceCount;
				state.PendingOperation = GroupedPolledSensorPendingOperation.EnableDisabled;
				_activeSensorStates[index] = state;
				if (_referenceCount++ == 0)
				{
					_sensorService._pollingScheduler.Acquire();
					_enableSignal.TrySetResult();
				}
			}
		}

		// This function is called by a sensor state to cancel grouped querying for it.
		// NB: The sensor state *WILL* ensure that this method is never called twice in succession for a given sensor.
		// NB: This method will only signal the state as to be cleaned up. Everything will happen as part of the polling loop.
		public void Release(IGroupedPolledSensorState state)
		{
			lock (_lock)
			{
				// If the sensor is still in the enable state, we must signal that to the polling loop for proper cleanup.
				state.PendingOperation = state.PendingOperation == GroupedPolledSensorPendingOperation.EnableDisabled ?
					GroupedPolledSensorPendingOperation.DisableNotEnabled :
					GroupedPolledSensorPendingOperation.DisableEnabled;
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
					catch (PollingSchedulerDisabledException) when (queryCancellationToken.IsCancellationRequested)
					{
						// NB: Generally, we should not see this exception.
					}
					catch (Exception ex)
					{
						_sensorService._groupedQueryStateLogger.SensorServiceGroupedQueryError(ex);
					}
					ClearAndDisposeCancellationTokenSource(ref _disableCancellationTokenSource);
					if (cancellationToken.IsCancellationRequested) return;
					Volatile.Write(ref _enableSignal, new(TaskCreationOptions.RunContinuationsAsynchronously));
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
			cancellationToken.ThrowIfCancellationRequested();
			while (true)
			{
				var now = DateTime.UtcNow;
				// TODO: See if it is still possible to avoid locking here. Seems complicated to avoid but maybe there is a way.
				lock (_lock)
				{
					// This loop will enable or disable sensors whose status has changed before requesting an update value.
					// That way, we avoid having concurrent calls of methods of the ISensorsGroupedQueryFeature interface and make implementations easier.
					// NB: There would generally be no problematic race conditions, but things become complex if implementations need to manage threads.
					// As such, it is better to enforce strict serialization of operations here.
					foreach (var state in _activeSensorStates)
					{
						// If we encounter a null value, this is the end of the list.
						// We could use the _referenceCount, but it would not avoid the need for a null check, and would require bounds-checking.
						if (state is null) break;
						if (state.PendingOperation != GroupedPolledSensorPendingOperation.None)
						{
							if (state.PendingOperation is GroupedPolledSensorPendingOperation.EnableDisabled)
							{
								_groupedQueryFeature.AddSensor(state.Sensor);
								state.PendingOperation = GroupedPolledSensorPendingOperation.None;
							}
							else
							{
								if (!ProcessRemove(state, state.PendingOperation != GroupedPolledSensorPendingOperation.DisableNotEnabled)) return;
								continue;
							}
						}
						state.RefreshDataPoint(now);
					}
				}
				cancellationToken.ThrowIfCancellationRequested();
				await _groupedQueryFeature.QueryValuesAsync(cancellationToken).ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
				await _sensorService._pollingScheduler.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		private bool ProcessRemove(IGroupedPolledSensorState state, bool wasEnabled)
		{
			state.PendingOperation = GroupedPolledSensorPendingOperation.None;
			int index = Array.IndexOf(_activeSensorStates, state, 0, _referenceCount);
			if (index < 0) throw new InvalidOperationException();
			bool isLast = --_referenceCount == 0;
			if (isLast)
			{
				ClearAndDisposeCancellationTokenSource(ref _disableCancellationTokenSource);
				_sensorService._pollingScheduler.Release();
			}
			else if ((uint)index < (uint)_referenceCount)
			{
				Array.Copy(_activeSensorStates, index + 1, _activeSensorStates, index, _referenceCount - index);
			}
			_activeSensorStates[_referenceCount] = null;
			if (wasEnabled) _groupedQueryFeature.RemoveSensor(state.Sensor);
			return !isLast;
		}
	}
}
