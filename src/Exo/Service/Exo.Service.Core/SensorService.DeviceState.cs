using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Exo.Configuration;

namespace Exo.Service;

internal sealed partial class SensorService
{
	private sealed class DeviceState
	{
		public AsyncLock Lock { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> SensorsConfigurationContainer { get; }
		public bool IsConnected { get; set; }
		public SensorDeviceInformation Information { get; set; }
		public GroupedQueryState? GroupedQueryState { get; set; }
		public Dictionary<Guid, SensorState>? SensorStates { get; set; }
		// Stores either a single signal in the form of a TaskCompletionSource or SensorArrivalTaskCompletionSource, or an array of those.
		// It is expected that in many cases, there will only be a single signal registered, if any at all, so this will avoid an unnecessary array allocation.
		private object? _arrivalSignals;

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> sensorsConfigurationContainer,
			bool isConnected,
			SensorDeviceInformation information,
			GroupedQueryState? groupedQueryState,
			Dictionary<Guid, SensorState>? sensorStates
		)
		{
			Lock = new();
			DeviceConfigurationContainer = deviceConfigurationContainer;
			SensorsConfigurationContainer = sensorsConfigurationContainer;
			IsConnected = isConnected;
			Information = information;
			GroupedQueryState = groupedQueryState;
			SensorStates = sensorStates;
		}

		public async ValueTask OnDeviceArrivalAsync(bool isConnected, SensorDeviceInformation information, GroupedQueryState? groupedQueryState, Dictionary<Guid, SensorState> sensorStates, CancellationToken cancellationToken)
		{
			using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				IsConnected = isConnected;
				Information = information;
				GroupedQueryState = groupedQueryState;
				SensorStates = sensorStates;

				var signals = _arrivalSignals;
				if (signals is not null)
				{
					if (signals is TaskCompletionSource[] array)
					{
						HandleArrival(array, sensorStates);
					}
					else
					{
						HandleArrival(Unsafe.As<TaskCompletionSource>(signals), sensorStates);
					}
				}
			}
		}

		private static void HandleArrival(SensorArrivalTaskCompletionSource taskCompletionSource, Dictionary<Guid, SensorState> sensorStates)
		{
			if (sensorStates.ContainsKey(taskCompletionSource.SensorId))
			{
				taskCompletionSource.TrySetResult();
			}
			else
			{
				taskCompletionSource.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new SensorNotAvailableException()));
			}
		}

		private static void HandleArrival(TaskCompletionSource taskCompletionSource, Dictionary<Guid, SensorState> sensorStates)
		{
			if (taskCompletionSource is SensorArrivalTaskCompletionSource sensorTaskCompletionSource)
			{
				HandleArrival(sensorTaskCompletionSource, sensorStates);
			}
			else
			{
				taskCompletionSource.TrySetResult();
			}
		}

		private static void HandleArrival(TaskCompletionSource[] taskCompletionSources, Dictionary<Guid, SensorState> sensorStates)
		{
			foreach (var tcs in taskCompletionSources)
			{
				HandleArrival(tcs, sensorStates);
			}
		}

		public async ValueTask WaitDeviceArrivalAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			TaskCompletionSource tcs;
			using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (IsConnected) return;

				tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				AddSignal(ref _arrivalSignals, tcs);
			}

			await using (cancellationToken.UnsafeRegister(tcs).ConfigureAwait(false))
			{
				await tcs.Task.ConfigureAwait(false);
			}
		}

		public async ValueTask WaitSensorArrivalAsync(Guid sensorId, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			SensorArrivalTaskCompletionSource tcs;
			using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (IsConnected) return;

				tcs = new SensorArrivalTaskCompletionSource(sensorId);
				AddSignal(ref _arrivalSignals, tcs);
			}

			await using (cancellationToken.UnsafeRegister(tcs).ConfigureAwait(false))
			{
				await tcs.Task.ConfigureAwait(false);
			}
		}

		// This is executed within the lock.
		private static void AddSignal(ref object? signals, TaskCompletionSource signal)
			=> signals = signals is null ?
				signal :
				signals is TaskCompletionSource[] array ?
					array.Add(signal) :
					[Unsafe.As<TaskCompletionSource>(signals), signal];
	}

	// A task completion source that holds context in the form of a Sensor ID.
	private sealed class SensorArrivalTaskCompletionSource : TaskCompletionSource
	{
		public Guid SensorId { get; }

		public SensorArrivalTaskCompletionSource(Guid sensorId) : base(TaskCreationOptions.RunContinuationsAsynchronously) => SensorId = sensorId;
	}
}
