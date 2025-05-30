using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Features.Sensors;
using Exo.Primitives;
using Exo.Sensors;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

// The sensor service manages everything related to sensors.
// It keeps a state for all devices an sensors, as well as manage polling of all sensors that are currently being watched.
// As such, it has to manage many background async tasks, which may make it a bit hard to understand.
// The core idea behind the many components inside the service is that querying sensor is optional and should not have an active cost when sensors are not read.
// Of course, this implied that starting or stopping to watch sensors requires a specific setup, but the watch operations should be efficient once running.
// Also of importance, it is built in a way so that slower or buggy drivers will not negatively impact the readings of other drivers.
// NB: To reduce clutter, most subtypes are located in other SensorService.*.cs files.
internal sealed partial class SensorService : IChangeSource<SensorDeviceInformation>, IChangeSource<SensorConfigurationUpdate>
{
	// Defaults to polling sensors once every second at most, as this seem to be a relatively standard way of doing.
	public const int PollingIntervalInMilliseconds = 1_000;

	// TODO: See if this can be merged with sensor states.
	// Currently, and contrary to other services, sensor states are cleared when the device is offline.
	// We would need to make it so the states stay online, but that could be problematic if the characteristics of a sensor change.
	private sealed class SensorConfiguration
	{
		public string? FriendlyName { get; set; }
		public bool IsFavorite { get; set; }

		public bool IsDefault => FriendlyName is null && !IsFavorite;

		public SensorConfiguration() { }

		public SensorConfiguration(PersistedSensorConfiguration value)
		{
			FriendlyName = value.FriendlyName;
			IsFavorite = value.IsFavorite;
		}

		public PersistedSensorConfiguration CreatePersistedSensorConfiguration()
			=> new() { FriendlyName = FriendlyName, IsFavorite = IsFavorite };
	}

	private static readonly Dictionary<Type, SensorDataType> TypeToSensorDataTypeMapping = new()
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

	private static class SensorDataTypes<T>
		where T : INumber<T>
	{
		public static readonly SensorDataType DataType = TypeToSensorDataTypeMapping[typeof(T)];
	}

	public static SensorDataType GetSensorDataType<T>() where T : INumber<T> => SensorDataTypes<T>.DataType;
	public static SensorDataType GetSensorDataType(Type type) => TypeToSensorDataTypeMapping[type];

	// Helper method that will ensure a cancellation token source is wiped out properly and exactly once. (Because the Dispose method can throw if called twice…)
	private static void ClearAndDisposeCancellationTokenSource(ref CancellationTokenSource? cancellationTokenSource)
	{
		if (Interlocked.Exchange(ref cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			cts.Dispose();
		}
	}

	private const string SensorsConfigurationContainerName = "sen";

	public static async ValueTask<SensorService> CreateAsync
	(
		ILoggerFactory loggerFactory,
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
			var sensorConfigurations = new Dictionary<Guid, SensorConfiguration>();

			foreach (var sensorId in sensorIds)
			{
				{
					var result = await sensorsConfigurationConfigurationContainer.ReadValueAsync(sensorId, SourceGenerationContext.Default.PersistedSensorInformation, cancellationToken).ConfigureAwait(false);
					if (!result.Found) continue;
					var info = result.Value;
					sensorInformations.Add(new SensorInformation(sensorId, info.DataType, info.Capabilities, info.UnitSymbol, info.ScaleMinimumValue, info.ScaleMaximumValue));
				}
				{
					var result = await sensorsConfigurationConfigurationContainer.ReadValueAsync(sensorId, SourceGenerationContext.Default.PersistedSensorConfiguration, cancellationToken).ConfigureAwait(false);
					sensorConfigurations.Add(sensorId, new SensorConfiguration(result.Found ? result.Value : new()));
				}
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
						false,
						sensorInformations.DrainToImmutable(),
						null,
						null,
						sensorConfigurations
					)
				);
			}
		}

		return new SensorService(loggerFactory, devicesConfigurationContainer, deviceWatcher, deviceStates);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates;
	private readonly AsyncLock _lock;
	private readonly PollingScheduler _pollingScheduler;
	private ChangeBroadcaster<SensorDeviceInformation> _sensorDeviceBroadcaster;
	private ChangeBroadcaster<SensorConfigurationUpdate> _configurationChangeBroadcaster;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly ILogger<SensorService> _logger;
	private readonly ILogger<SensorState> _sensorStateLogger;
	private readonly ILogger<GroupedQueryState> _groupedQueryStateLogger;
	private readonly IDeviceWatcher _deviceWatcher;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _sensorDeviceWatchTask;

	private SensorService(ILoggerFactory loggerFactory, IConfigurationContainer<Guid> devicesConfigurationContainer, IDeviceWatcher deviceWatcher, ConcurrentDictionary<Guid, DeviceState> deviceStates)
	{
		_deviceStates = deviceStates;
		_lock = new();
		_pollingScheduler = new(loggerFactory.CreateLogger<PollingScheduler>(), TimeSpan.FromTicks(PollingIntervalInMilliseconds * TimeSpan.TicksPerMillisecond));
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_logger = loggerFactory.CreateLogger<SensorService>();
		_sensorStateLogger = loggerFactory.CreateLogger<SensorState>();
		_groupedQueryStateLogger = loggerFactory.CreateLogger<GroupedQueryState>();
		_deviceWatcher = deviceWatcher;
		_cancellationTokenSource = new();
		_sensorDeviceWatchTask = WatchSensorDevicesAsync(_cancellationTokenSource.Token);
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
					await DetachDeviceStateAsync(state).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
			}
			await _pollingScheduler.DisposeAsync();
			cts.Dispose();
		}
	}

	private async Task WatchSensorDevicesAsync(CancellationToken cancellationToken)
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

		try
		{
			var sensorInfos = new SensorInformation[sensors.Length];
			var sensorStates = new Dictionary<Guid, SensorState>();
			var addedSensorInfosById = new Dictionary<Guid, SensorInformation>();
			for (int i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];
				if (!sensorStates.TryAdd(sensor.SensorId, SensorState.Create(_sensorStateLogger, this, groupedQueryState, sensor)))
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

					var sensorConfigurations = new Dictionary<Guid, SensorConfiguration>();

					// For sanity, remove the pre-existing sensor containers, although there should be none initially.
					await sensorsContainer.DeleteAllContainersAsync().ConfigureAwait(false);
					foreach (var info in sensorInfos)
					{
						await sensorsContainer.WriteValueAsync(info.SensorId, new PersistedSensorInformation(info), SourceGenerationContext.Default.PersistedSensorInformation, cancellationToken);
						sensorConfigurations.Add(info.SensorId, new());
					}

					state = new
					(
						deviceContainer,
						sensorsContainer,
						notification.DeviceInformation.IsAvailable,
						ImmutableCollectionsMarshal.AsImmutableArray(sensorInfos),
						groupedQueryState,
						sensorStates,
						sensorConfigurations
					);

					_deviceStates.TryAdd(notification.DeviceInformation.Id, state);
				}
				else
				{
					using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
					{
						sensorsContainer = state.SensorsConfigurationContainer;
						var sensorConfigurations = state.SensorConfigurations;

						foreach (var previousInfo in state.Sensors)
						{
							// Remove all pre-existing sensor info from the dictionary that was build earlier so that only new entries remain in the end.
							// Appropriate updates for previous sensors will be done depending on the result of that removal.
							if (!addedSensorInfosById.Remove(previousInfo.SensorId, out var currentInfo))
							{
								// Remove existing sensor configuration if the sensor is not reported by the device anymore.
								await sensorsContainer.DeleteValuesAsync(previousInfo.SensorId).ConfigureAwait(false);
								sensorConfigurations.Remove(previousInfo.SensorId);
							}
							else if (currentInfo != previousInfo)
							{
								// Only update the information if it has changed since the last time. (Do not wear the disk with useless writes)
								await sensorsContainer.WriteValueAsync(currentInfo.SensorId, new PersistedSensorInformation(currentInfo), SourceGenerationContext.Default.PersistedSensorInformation, cancellationToken).ConfigureAwait(false);
							}
						}

						// Finally, persist the information for the newly discovered sensors.
						foreach (var currentInfo in addedSensorInfosById.Values)
						{
							await sensorsContainer.WriteValueAsync(currentInfo.SensorId, new PersistedSensorInformation(currentInfo), SourceGenerationContext.Default.PersistedSensorInformation, cancellationToken).ConfigureAwait(false);
							sensorConfigurations.Add(currentInfo.SensorId, new());
						}

						await state.OnDeviceArrivalAsync
						(
							notification.DeviceInformation.IsAvailable,
							ImmutableCollectionsMarshal.AsImmutableArray(sensorInfos),
							groupedQueryState,
							sensorStates,
							cancellationToken
						).ConfigureAwait(false);
					}
				}
				// NB: There is no need to transmit any sensor change information, as all new sensors start with an empty configuration.
				// We do however need to transmit sensor information every time so that the connected status is known.
				var sensorDeviceBroadcaster = _sensorDeviceBroadcaster.GetSnapshot();
				if (!sensorDeviceBroadcaster.IsEmpty) sensorDeviceBroadcaster.Push(state.CreateInformation(notification.DeviceInformation.Id));
			}
		}
		catch (Exception)
		{
			if (groupedQueryState is not null)
			{
				await groupedQueryState.DisposeAsync().ConfigureAwait(false);
			}
			throw;
		}
	}

	private static SensorInformation BuildSensorInformation(ISensor sensor)
	{
		var dataType = TypeToSensorDataTypeMapping[sensor.ValueType];

		return dataType switch
		{
			SensorDataType.UInt8 => BuildSensorInformation<byte>(sensor, dataType),
			SensorDataType.UInt16 => BuildSensorInformation<ushort>(sensor, dataType),
			SensorDataType.UInt32 => BuildSensorInformation<uint>(sensor, dataType),
			SensorDataType.UInt64 => BuildSensorInformation<ulong>(sensor, dataType),
			SensorDataType.UInt128 => BuildSensorInformation<UInt128>(sensor, dataType),
			SensorDataType.SInt8 => BuildSensorInformation<sbyte>(sensor, dataType),
			SensorDataType.SInt16 => BuildSensorInformation<short>(sensor, dataType),
			SensorDataType.SInt32 => BuildSensorInformation<int>(sensor, dataType),
			SensorDataType.SInt64 => BuildSensorInformation<long>(sensor, dataType),
			SensorDataType.SInt128 => BuildSensorInformation<Int128>(sensor, dataType),
			SensorDataType.Float16 => BuildSensorInformation<Half>(sensor, dataType),
			SensorDataType.Float32 => BuildSensorInformation<float>(sensor, dataType),
			SensorDataType.Float64 => BuildSensorInformation<double>(sensor, dataType),
			_ => throw new InvalidOperationException(),
		};
	}

	private static SensorInformation BuildSensorInformation<T>(ISensor sensor, SensorDataType dataType)
		where T : unmanaged, INumber<T>
		=> BuildSensorInformation((ISensor<T>)sensor, dataType);

	private static SensorInformation BuildSensorInformation<T>(ISensor<T> sensor, SensorDataType dataType)
		where T : unmanaged, INumber<T>
	{
		var capabilities = GetCapabilities(sensor);
		VariantNumber minimumValue;
		VariantNumber maximumValue;
		if (sensor.ScaleMinimumValue is not null)
		{
			capabilities |= SensorCapabilities.HasMinimumValue;
			minimumValue = VariantNumber.Create(sensor.ScaleMinimumValue.GetValueOrDefault());
		}
		else
		{
			minimumValue = default;
		}
		if (sensor.ScaleMaximumValue is not null)
		{
			capabilities |= SensorCapabilities.HasMaximumValue;
			maximumValue = VariantNumber.Create(sensor.ScaleMaximumValue.GetValueOrDefault());
		}
		else
		{
			maximumValue = default;
		}
		return new SensorInformation(sensor.SensorId, dataType, capabilities, sensor.Unit.Symbol, minimumValue, maximumValue);
	}

	private static SensorCapabilities GetCapabilities(ISensor sensor)
	{
		SensorCapabilities capabilities = SensorCapabilities.None;

		switch (sensor.Kind)
		{
			case SensorKind.Internal: break;
			case SensorKind.Polled: capabilities |= SensorCapabilities.Polled; break;
			case SensorKind.Streamed: capabilities |= SensorCapabilities.Streamed; break;
			default: throw new InvalidOperationException("Unsupported enum value.");
		}

		return capabilities;
	}

	private async ValueTask HandleRemovalAsync(DeviceWatchNotification notification)
	{
		if (!_deviceStates.TryGetValue(notification.DeviceInformation.Id, out var state)) return;

		await DetachDeviceStateAsync(state).ConfigureAwait(false);

		// NB: The "static" information is only updated within the main lock, so we don't need to do this in the state lock.
		// (This is done similarly during arrival)
		var sensorDeviceBroadcaster = _sensorDeviceBroadcaster.GetSnapshot();
		if (!sensorDeviceBroadcaster.IsEmpty) sensorDeviceBroadcaster.Push(state.CreateInformation(notification.DeviceInformation.Id));
	}

	private async ValueTask DetachDeviceStateAsync(DeviceState state)
	{
		using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			await state.OnDeviceRemovalAsync().ConfigureAwait(false);
		}
	}

	async ValueTask<SensorDeviceInformation[]?> IChangeSource<SensorDeviceInformation>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<SensorDeviceInformation> writer, CancellationToken cancellationToken)
	{
		List<SensorDeviceInformation>? initialDeviceInfos = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var (deviceId, deviceState) in _deviceStates)
			{
				(initialDeviceInfos ??= []).Add(deviceState.CreateInformation(deviceId));
			}
			_sensorDeviceBroadcaster.Register(writer);
		}
		return initialDeviceInfos?.ToArray();
	}

	void IChangeSource<SensorDeviceInformation>.UnsafeUnregisterWatcher(ChannelWriter<SensorDeviceInformation> writer)
		=> _sensorDeviceBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<SensorDeviceInformation>.SafeUnregisterWatcherAsync(ChannelWriter<SensorDeviceInformation> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_sensorDeviceBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	async ValueTask<SensorConfigurationUpdate[]?> IChangeSource<SensorConfigurationUpdate>.GetInitialChangesAndRegisterWatcherAsync(ChannelWriter<SensorConfigurationUpdate> writer, CancellationToken cancellationToken)
	{
		List<SensorConfigurationUpdate>? initialUpdates = null;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var (deviceId, deviceState) in _deviceStates)
			{
				foreach (var (sensorId, sensorConfiguration) in deviceState.SensorConfigurations)
				{
					// We should not need to push information for any default configuration, as all sensors default to an empty configuration. (Configuration is purely user-controlled)
					if (sensorConfiguration.IsDefault) continue;
					(initialUpdates ??= []).Add
					(
						new SensorConfigurationUpdate()
						{
							DeviceId = deviceId,
							SensorId = sensorId,
							FriendlyName = sensorConfiguration.FriendlyName,
							IsFavorite = sensorConfiguration.IsFavorite
						}
					);
				}
			}
			_configurationChangeBroadcaster.Register(writer);
		}
		return initialUpdates?.ToArray();
	}

	void IChangeSource<SensorConfigurationUpdate>.UnsafeUnregisterWatcher(ChannelWriter<SensorConfigurationUpdate> writer)
		=> _configurationChangeBroadcaster.Unregister(writer);

	async ValueTask IChangeSource<SensorConfigurationUpdate>.SafeUnregisterWatcherAsync(ChannelWriter<SensorConfigurationUpdate> writer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			_configurationChangeBroadcaster.Unregister(writer);
			writer.TryComplete();
		}
	}

	public async ValueTask<IChangeSource<SensorDataPoint<TValue>>> GetValueWatcherAsync<TValue>(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
		where TValue : struct, INumber<TValue>
	{
		if (!_deviceStates.TryGetValue(deviceId, out var state)) throw new DeviceNotFoundException();
		using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (state.SensorStates is null || !state.SensorStates.TryGetValue(sensorId, out var sensorState)) throw new SensorNotFoundException();

			// NB: This can throw InvalidCastException if TValue is not correct, which is intended behavior.
			return (IChangeSource<SensorDataPoint<TValue>>)sensorState;
		}
	}

	public async ValueTask<(SensorDataType, object)> GetValueWatcherAsync(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var state)) throw new DeviceNotFoundException();
		using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (state.SensorStates is null || !state.SensorStates.TryGetValue(sensorId, out var sensorState)) throw new SensorNotFoundException();

			// NB: This can throw InvalidCastException if TValue is not correct, which is intended behavior.
			return (sensorState.DataType, sensorState);
		}
	}

	public bool TryGetSensorInformation(Guid deviceId, Guid sensorId, out SensorInformation info)
	{
		if (_deviceStates.TryGetValue(deviceId, out var state))
		{
			foreach (var sensor in state.Sensors)
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

	// Waits for arrival of a sensor.
	// This method will throw an exception if the sensor has become unavailable, so that we can detect problems with the setup.
	public async ValueTask WaitForSensorAsync(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		await deviceState.WaitSensorArrivalAsync(sensorId, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask<SensorInformation> GetSensorInformationAsync(Guid deviceId, Guid sensorId, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var sensor in deviceState.Sensors)
			{
				if (sensor.SensorId == sensorId) return sensor;
			}
		}

		throw new SensorNotAvailableException();
	}

	public async ValueTask SetFavoriteAsync(Guid deviceId, Guid sensorId, bool isFavorite, CancellationToken cancellationToken)
	{
		if (!_deviceStates.TryGetValue(deviceId, out var deviceState)) throw new DeviceNotFoundException();

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (deviceState.SensorConfigurations.TryGetValue(sensorId, out var sensorConfiguration))
			{
				if (sensorConfiguration.IsFavorite != isFavorite)
				{
					sensorConfiguration.IsFavorite = isFavorite;

					if (_configurationChangeBroadcaster is { } changeListeners)
					{
						changeListeners.Push(new() { DeviceId = deviceId, SensorId = sensorId, FriendlyName = sensorConfiguration.FriendlyName, IsFavorite = sensorConfiguration.IsFavorite });
					}

					await deviceState.SensorsConfigurationContainer.WriteValueAsync(sensorId, sensorConfiguration.CreatePersistedSensorConfiguration(), SourceGenerationContext.Default.PersistedSensorConfiguration, cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}
}
