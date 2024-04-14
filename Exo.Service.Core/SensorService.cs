using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

// The sensor service manages everything related to sensors.
// It keeps a state for all devices an sensors, as well as manage polling of all sensors that are currently being watched.
// As such, it has to manage many background async tasks, which may make it a bit hard to understand.
// The core idea behind the many components inside the service is that querying sensor is optional and should not have an active cost when sensors are not read.
// Of course, this implied that starting or stopping to watch sensors requires a specific setup, but the watch operations should be efficient once running.
// Also of importance, it is built in a way so that slower or buggy drivers will not negatively impact the readings of other drivers.
// NB: To reduce clutter, most subtypes are located in other SensorService.*.cs files.
public sealed partial class SensorService
{
	[TypeId(0x7757FFB0, 0x6111, 0x4DB1, 0xBC, 0xFC, 0x70, 0x97, 0x38, 0xF3, 0xC6, 0x34)]
	private readonly struct PersistedSensorInformation
	{
		public PersistedSensorInformation(SensorInformation info)
		{
			DataType = info.DataType;
			UnitSymbol = info.Unit;
			IsPolled = info.IsPolled;
		}

		public SensorDataType DataType { get; init; }
		public string UnitSymbol { get; init; }
		public bool IsPolled { get; init; }
	}

	private static readonly Dictionary<Type, SensorDataType> SensorDataTypes = new()
	{
		{ typeof(byte), SensorDataType.UInt8 },
		{ typeof(ushort), SensorDataType.UInt16 },
		{ typeof(uint), SensorDataType.UInt32 },
		{ typeof(ulong), SensorDataType.UInt64 },
		{ typeof(UInt128), SensorDataType.UInt128 },
		{ typeof(sbyte), SensorDataType.SInt8 },
		{ typeof(short), SensorDataType.SInt16 },
		{ typeof(int), SensorDataType.SInt32 },
		{ typeof(long), SensorDataType.SInt64 },
		{ typeof(Int128), SensorDataType.SInt128 },
		{ typeof(Half), SensorDataType.Float16 },
		{ typeof(float), SensorDataType.Float32 },
		{ typeof(double), SensorDataType.Float64 },
	};

	private const string SensorsConfigurationContainerName = "sen";

	public static async ValueTask<SensorService> CreateAsync
	(
		ILogger<SensorService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			if (deviceConfigurationContainer.TryGetContainer(SensorsConfigurationContainerName, GuidNameSerializer.Instance) is not { } sensorsConfigurationConfigurationContainer)
			{
				continue;
			}

			var sensorIds = await sensorsConfigurationConfigurationContainer.GetKeysAsync(cancellationToken);

			if (sensorIds.Length == 0)
			{
				continue;
			}

			var sensorInformations = ImmutableArray.CreateBuilder<SensorInformation>();

			foreach (var sensorId in sensorIds)
			{
				var result = await sensorsConfigurationConfigurationContainer.ReadValueAsync<PersistedSensorInformation>(sensorId, cancellationToken).ConfigureAwait(false);
				if (!result.Found) continue;
				var info = result.Value;
				sensorInformations.Add(new SensorInformation(sensorId, info.DataType, info.UnitSymbol, info.IsPolled));
			}

			if (sensorInformations.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					new DeviceState
					(
						deviceConfigurationContainer,
						sensorsConfigurationConfigurationContainer,
						new(deviceId, sensorInformations.DrainToImmutable()),
						null,
						null
					)
				);
			}
		}

		return new SensorService(logger, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private readonly PollingScheduler _pollingScheduler;
	private ChannelWriter<SensorDeviceInformation>[]? _changeListeners;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly ILogger<SensorService> _logger;
	private readonly IDeviceWatcher _deviceWatcher;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _sensorDeviceWatchTask;

	private SensorService(ILogger<SensorService> logger, IConfigurationContainer<Guid> devicesConfigurationContainer, IDeviceWatcher deviceWatcher, ConcurrentDictionary<Guid, DeviceState> deviceStates)
	{
		_deviceStates = deviceStates;
		_lock = new();
		_pollingScheduler = new(500);
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_logger = logger;
		_deviceWatcher = deviceWatcher;
		_cancellationTokenSource = new();
		_sensorDeviceWatchTask = WatchSensorsDevicesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _sensorDeviceWatchTask.ConfigureAwait(false);
			// It is important to stop any background process running as part of the device states here.
			foreach (var state in _deviceStates.Values)
			{
				try
				{
					using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
					{
						await DetachDeviceStateAsync(state).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
			_pollingScheduler.Dispose();
			cts.Dispose();
		}
	}

	private async Task WatchSensorsDevicesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ISensorDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Enumeration:
					case WatchNotificationKind.Addition:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						}
						break;
					case WatchNotificationKind.Removal:
						using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
						{
							// NB: Removal should not be cancelled. We need all the states to be cleared away.
							await HandleRemovalAsync(notification).ConfigureAwait(false);
						}
						break;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		ImmutableArray<ISensor> sensors;
		var sensorFeatures = (IDeviceFeatureSet<ISensorDeviceFeature>)notification.FeatureSet!;
		sensors = sensorFeatures.GetFeature<ISensorsFeature>() is { } sensorsFeature ? sensorsFeature.Sensors : [];
		var groupedQueryState = sensorFeatures.GetFeature<ISensorsGroupedQueryFeature>() is { } groupedQueryFeature ? new GroupedQueryState(this, groupedQueryFeature, sensors.Length) : null;
		var sensorInfos = new SensorInformation[sensors.Length];

		var sensorStates = new Dictionary<Guid, SensorState>();
		var addedSensorInfosById = new Dictionary<Guid, SensorInformation>();
		for (int i = 0; i < sensors.Length; i++)
		{
			var sensor = sensors[i];
			if (!sensorStates.TryAdd(sensor.SensorId, SensorState.Create(this, groupedQueryState, sensor)))
			{
				// We ignore all sensors and discard the device if there is a duplicate ID.
				// TODO: Log an error.
				sensorInfos = [];
				sensorStates.Clear();
				break;
			}
			var info = BuildSensorInformation(sensor);
			addedSensorInfosById.Add(info.SensorId, info);
			sensorInfos[i] = info;
		}

		if (sensorInfos.Length == 0)
		{
			if (_deviceStates.TryRemove(notification.DeviceInformation.Id, out var state))
			{
				await state.SensorsConfigurationContainer.DeleteAllContainersAsync().ConfigureAwait(false);
			}
		}
		else
		{
			IConfigurationContainer<Guid> sensorsContainer;
			if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state))
			{
				var deviceContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
				sensorsContainer = deviceContainer.GetContainer(SensorsConfigurationContainerName, GuidNameSerializer.Instance);

				// For sanity, remove the pre-existing sensor containers, although there should be none initially.
				await sensorsContainer.DeleteAllContainersAsync().ConfigureAwait(false);
				foreach (var info in sensorInfos)
				{
					await sensorsContainer.WriteValueAsync(info.SensorId, new PersistedSensorInformation(info), cancellationToken);
				}

				state = new(deviceContainer, sensorsContainer, new(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(sensorInfos)), groupedQueryState, sensorStates);
			}
			else
			{
				sensorsContainer = state.SensorsConfigurationContainer;

				foreach (var previousInfo in state.Information.Sensors)
				{
					// Remove all pre-existing sensor info from the dictionary that was build earlier so that only new entries remain in the end.
					// Appropriate updates for previous sensors will be done depending on the result of that removal.
					if (!addedSensorInfosById.Remove(previousInfo.SensorId, out var currentInfo))
					{
						// Remove existing sensor configuration if the sensor is not reported by the device anymore.
						await sensorsContainer.DeleteValuesAsync(previousInfo.SensorId).ConfigureAwait(false);
					}
					else if (currentInfo != previousInfo)
					{
						// Only update the information if it has changed since the last time. (Do not wear the disk with useless writes)
						await sensorsContainer.WriteValueAsync(currentInfo.SensorId, new PersistedSensorInformation(currentInfo), cancellationToken).ConfigureAwait(false);
					}
				}

				// Finally, persist the information for the newly discovered sensors.
				foreach (var currentInfo in addedSensorInfosById.Values)
				{
					await sensorsContainer.WriteValueAsync(currentInfo.SensorId, new PersistedSensorInformation(currentInfo), cancellationToken).ConfigureAwait(false);
				}

				using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					state.Information = new SensorDeviceInformation(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(sensorInfos));
					state.GroupedQueryState = groupedQueryState;
					state.SensorStates = sensorStates;
				}
			}
			_changeListeners.TryWrite(state.Information);
		}
	}

	private static SensorInformation BuildSensorInformation(ISensor sensor) => new SensorInformation(sensor.SensorId, SensorDataTypes[sensor.ValueType], sensor.Unit.Symbol, sensor.IsPolled);

	private async ValueTask HandleRemovalAsync(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state)) return;

		await DetachDeviceStateAsync(state).ConfigureAwait(false);
	}

	private async ValueTask DetachDeviceStateAsync(DeviceState state)
	{
		using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			state.IsConnected = false;
			if (state.GroupedQueryState is { } groupedQueryState)
			{
				await groupedQueryState.DisposeAsync().ConfigureAwait(false);
			}
			state.GroupedQueryState = null;
			if (state.SensorStates is { } sensorStates)
			{
				foreach (var sensorState in sensorStates.Values)
				{
					await sensorState.DisposeAsync().ConfigureAwait(false);
				}
			}
			state.SensorStates = null;
		}
	}

	public async IAsyncEnumerable<SensorDeviceInformation> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<SensorDeviceInformation>();

		SensorDeviceInformation[]? initialDeviceInfos = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			initialDeviceInfos = _deviceStates.Values.Select(state => state.Information).ToArray();
			ArrayExtensions.InterlockedAdd(ref _changeListeners, channel);
		}
		try
		{
			foreach (var info in initialDeviceInfos)
			{
				yield return info;
			}
			initialDeviceInfos = null;

			await foreach (var info in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return info;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _changeListeners, channel);
		}
	}

	public async IAsyncEnumerable<SensorDataPoint<TValue>> WatchValuesAsync<TValue>(Guid deviceId, Guid sensorId, [EnumeratorCancellation] CancellationToken cancellationToken)
		where TValue : struct, INumber<TValue>
	{
		if (!_deviceStates.TryGetValue(deviceId, out var state)) yield break;
		IAsyncEnumerable<SensorDataPoint<TValue>> dataPointEnumerable;
		using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (state.SensorStates is null) yield break;
			if (!state.SensorStates.TryGetValue(sensorId, out var sensorState)) yield break;

			// NB: This can throw InvalidCastException if TValue is not correct, which is intended behavior.
			dataPointEnumerable = ((SensorState<TValue>)sensorState).WatchAsync(cancellationToken);
		}
		await foreach (var dataPoint in dataPointEnumerable.ConfigureAwait(false))
		{
			yield return dataPoint;
		}
	}

	public bool TryGetSensorInformation(Guid deviceId, Guid sensorId, out SensorInformation info)
	{
		if (_deviceStates.TryGetValue(deviceId, out var state))
		{
			foreach (var sensor in state.Information.Sensors)
			{
				if (sensor.SensorId == sensorId)
				{
					info = sensor;
					return true;
				}
			}
		}
		info = default;
		return false;
	}
}
