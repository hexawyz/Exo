using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Exo.Configuration;
using Exo.Features;
using Exo.Sensors;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public sealed class SensorService
{
	[TypeId(0x7757FFB0, 0x6111, 0x4DB1, 0xBC, 0xFC, 0x70, 0x97, 0x38, 0xF3, 0xC6, 0x34)]
	private readonly struct PersistedSensorInformation
	{
		public PersistedSensorInformation(SensorInformation info)
		{
			DataType = info.DataType;
			IsPolled = info.IsPolled;
		}

		public SensorDataType DataType { get; init; }
		public bool IsPolled { get; init; }
	}

	private sealed class SensorState
	{
		public SensorState(ISensor sensor) => Sensor = sensor;

		public ISensor Sensor { get; }
	}

	private sealed class DeviceState
	{
		public AsyncLock Lock { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> SensorsConfigurationContainer { get; }
		public bool IsConnected { get; set; }
		public SensorDeviceInformation Information { get; set; }
		public Dictionary<Guid, SensorState>? SensorStates { get; set; }

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> sensorsConfigurationContainer,
			SensorDeviceInformation information,
			Dictionary<Guid, SensorState>? sensorStates
		)
		{
			Lock = new();
			DeviceConfigurationContainer = deviceConfigurationContainer;
			SensorsConfigurationContainer = sensorsConfigurationContainer;
			Information = information;
			SensorStates = sensorStates;
		}
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
				sensorInformations.Add(new SensorInformation(sensorId, info.DataType, info.IsPolled));
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
						null
					)
				);
			}
		}

		return new SensorService(logger, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly ILogger<SensorService> _logger;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly IDeviceWatcher _deviceWatcher;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _sensorDeviceWatchTask;

	private SensorService(ILogger<SensorService> logger, IConfigurationContainer<Guid> devicesConfigurationContainer, IDeviceWatcher deviceWatcher, ConcurrentDictionary<Guid, DeviceState> deviceStates)
	{
		_deviceStates = deviceStates;
		_logger = logger;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_deviceWatcher = deviceWatcher;
		_cancellationTokenSource = new();
		_sensorDeviceWatchTask = WatchSensorsAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			await _sensorDeviceWatchTask.ConfigureAwait(false);
			cts.Dispose();
		}
	}

	private async Task WatchSensorsAsync(CancellationToken cancellationToken)
	{
		// This method is used to automatically register and unregister the I2C implementations of display adapters that will be used by monitor drivers.
		var busRegistrations = new Dictionary<Guid, IDisposable>();
		try
		{
			var settings = new List<MonitorSetting>();

			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<ISensorDeviceFeature>(cancellationToken))
			{
				try
				{
					switch (notification.Kind)
					{
					case WatchNotificationKind.Addition:
						await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						break;
					case WatchNotificationKind.Removal:
						await HandleRemovalAsync(notification, cancellationToken).ConfigureAwait(false);
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
		var sensorInfos = new SensorInformation[sensors.Length];

		var sensorStates = new Dictionary<Guid, SensorState>();
		var addedSensorInfosById = new Dictionary<Guid, SensorInformation>();
		for (int i = 0; i < sensors.Length; i++)
		{
			var sensor = sensors[i];
			if (!sensorStates.TryAdd(sensor.SensorId, new(sensor)))
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

				state = new(deviceContainer, sensorsContainer, new(notification.DeviceInformation.Id, ImmutableCollectionsMarshal.AsImmutableArray(sensorInfos)), sensorStates);
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
				}
			}
		}
	}

	private static SensorInformation BuildSensorInformation(ISensor sensor) => new SensorInformation(sensor.SensorId, SensorDataTypes[sensor.ValueType], sensor.IsPolled);

	private async ValueTask HandleRemovalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state)) return;

		using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			state.IsConnected = false;
			state.SensorStates = null;
		}
	}
}
