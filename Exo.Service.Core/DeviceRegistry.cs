using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DeviceTools;
using Exo.Features;

namespace Exo.Service;

public sealed class DeviceRegistry : IDriverRegistry, IInternalDriverRegistry, IDeviceWatcher
{
	private enum UpdateKind : byte
	{
		Other = 0,
		AddedDevice = 1,
		RemovedDevice = 2,
		ConnectedDevice = 3,
		DisconnectedDevice = 4,
	}

	[TypeId(0x91731318, 0x06F0, 0x402E, 0x8F, 0x6F, 0x23, 0x3D, 0x8C, 0x4D, 0x8F, 0xE5)]
	private record class IndexedConfigurationKey
	{
		public IndexedConfigurationKey(DeviceConfigurationKey key, int instanceIndex)
			: this(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.SerialNumber, instanceIndex) { }

		[JsonConstructor]
		public IndexedConfigurationKey(string driverKey, string deviceMainId, string compatibleHardwareId, string? serialNumber, int instanceIndex)
		{
			DriverKey = driverKey;
			DeviceMainId = deviceMainId;
			CompatibleHardwareId = compatibleHardwareId;
			SerialNumber = serialNumber;
			InstanceIndex = instanceIndex;
		}

		public string DriverKey { get; }
		public string DeviceMainId { get; }
		public string CompatibleHardwareId { get; }
		public string? SerialNumber { get; }
		public int InstanceIndex { get; }

		public static explicit operator DeviceConfigurationKey(IndexedConfigurationKey key)
			=> new(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.SerialNumber);
	}

	private sealed class DeviceState
	{
		public DeviceState(Guid deviceId, IndexedConfigurationKey configurationKey, DeviceInformation deviceInformation, DeviceUserConfiguration userConfiguration)
		{
			DeviceId = deviceId;
			_configurationKey = configurationKey;
			_deviceInformation = deviceInformation;
			_userConfiguration = userConfiguration;
		}

		public Guid DeviceId { get; }

		private IndexedConfigurationKey _configurationKey;
		public IndexedConfigurationKey ConfigurationKey
		{
			get => Volatile.Read(ref _configurationKey);
			set => Volatile.Write(ref _configurationKey, value);
		}

		private DeviceInformation _deviceInformation;
		public DeviceInformation DeviceInformation
		{
			get => Volatile.Read(ref _deviceInformation);
			set => Volatile.Write(ref _deviceInformation, value);
		}

		private DeviceUserConfiguration _userConfiguration;
		public DeviceUserConfiguration UserConfiguration
		{
			get => Volatile.Read(ref _userConfiguration);
			set => Volatile.Write(ref _userConfiguration, value);
		}

		private Driver? _driver;
		public Driver? Driver => Volatile.Read(ref _driver);

		public bool TrySetDriver(Driver driver) => Interlocked.CompareExchange(ref _driver, driver, null) is null;

		public bool TryUnsetDriver() => Interlocked.Exchange(ref _driver, null) is not null;

		public DeviceStateInformation GetDeviceStateInformation()
			=> new
			(
				DeviceId,
				DeviceInformation.FriendlyName,
				UserConfiguration.FriendlyName,
				DeviceInformation.Category,
				DeviceInformation.FeatureIds,
				DeviceInformation.DeviceIds,
				DeviceInformation.MainDeviceIdIndex,
				DeviceInformation.SerialNumber,
				_driver is not null
			);
	}

	private sealed class DriverConfigurationState
	{
		public string Key { get; }
		public Dictionary<string, Guid> DeviceIdsBySerialNumber { get; } = new();
		public Dictionary<string, Guid> DeviceIdsByMainDeviceName { get; } = new();
		public Dictionary<string, Dictionary<Guid, int>> DeviceIdsByCompatibleHardwareId { get; } = new();

		public DriverConfigurationState(string key) => Key = key;

		// This supports upgrading from no serial number to serial number for now, but it is probably not a desirable feature in the long run.
		// Only trouble is to ensure that the main device ID does not get duplicated between two devices. Some proper and well-established resolution procedure is required.
		// TODO: Use the device states instead of GUIDs (if possible?) so that the serial number can only be upgraded from "no serial number".
		public bool TryGetDeviceId(in DeviceConfigurationKey key, out Guid deviceId)
			=> key.SerialNumber is not null ?
				DeviceIdsBySerialNumber.TryGetValue(key.SerialNumber, out deviceId) || DeviceIdsByMainDeviceName.TryGetValue(key.DeviceMainId, out deviceId) :
				DeviceIdsByMainDeviceName.TryGetValue(key.DeviceMainId, out deviceId);

		public IndexedConfigurationKey RegisterDevice(in Guid deviceId, in DeviceConfigurationKey key)
		{
			if (key.SerialNumber is not null)
			{
				if (!DeviceIdsBySerialNumber.TryAdd(key.SerialNumber, deviceId))
				{
					DeviceIdsBySerialNumber.TryGetValue(key.SerialNumber, out var otherDeviceId);
					throw new InvalidOperationException($"A non unique serial number was provided for devices {otherDeviceId:B} and {deviceId:B}.");
				}
			}

			if (!DeviceIdsByMainDeviceName.TryAdd(key.DeviceMainId, deviceId))
			{
				DeviceIdsByMainDeviceName.TryGetValue(key.DeviceMainId, out var otherDeviceId);
				throw new InvalidOperationException($"The same main device name was found for devices {otherDeviceId:B} and {deviceId:B}.");
			}
			else if (!DeviceIdsByCompatibleHardwareId.TryGetValue(key.CompatibleHardwareId, out var devicesWithSameHardwareId))
			{
				DeviceIdsByCompatibleHardwareId.TryAdd(key.CompatibleHardwareId, devicesWithSameHardwareId = new());
				// TODO: Not actually sure yet what should be the data structure there. (We probably don't want the configurations to be migrated automatically, so this can be done later)
			}

			return new IndexedConfigurationKey(key, 0);
		}

		public IndexedConfigurationKey UpdateDeviceConfigurationKey(in Guid deviceId, in DeviceConfigurationKey oldKey, in DeviceConfigurationKey newKey)
		{
			if (oldKey.SerialNumber != newKey.SerialNumber)
			{
				if (oldKey.SerialNumber is not null)
				{
					DeviceIdsBySerialNumber.Remove(oldKey.SerialNumber);
				}
				if (newKey.SerialNumber is not null)
				{
					DeviceIdsBySerialNumber.Add(newKey.SerialNumber, deviceId);
				}
			}
			if (oldKey.DeviceMainId != newKey.DeviceMainId)
			{
				DeviceIdsByMainDeviceName.Remove(oldKey.DeviceMainId);
				DeviceIdsByMainDeviceName.Add(newKey.DeviceMainId, deviceId);
			}
			if (oldKey.CompatibleHardwareId != newKey.CompatibleHardwareId)
			{
				// TODO
			}
			return new(newKey, 0);
		}

		public IndexedConfigurationKey GetIndexedKey(in Guid deviceId, in DeviceConfigurationKey key)
		{
			// TODO
			return new(key, 0);
		}
	}

	private static readonly ConditionalWeakTable<Type, Type[]> DriverFeatureCache = new();

	private static Type[] GetDriverFeatures(Type driverType)
		=> DriverFeatureCache.GetValue(driverType, GetNonCachedDriverFeatures);

	private static Type[] GetNonCachedDriverFeatures(Type driverType)
	{
		var featureTypes = new List<Type>();
		foreach (var interfaceType in driverType.GetInterfaces())
		{
			var t = interfaceType;
			while (t.BaseType is not null)
			{
				t = t.BaseType;
			}

			if (t.IsGenericType && t != typeof(IDeviceDriver<IDeviceFeature>) && t.GetGenericTypeDefinition() == typeof(IDeviceDriver<>))
			{
				featureTypes.Add(t.GetGenericArguments()[0]);
			}
		}

		return featureTypes.ToArray();
	}

	public static async Task<DeviceRegistry> CreateAsync(ConfigurationService configurationService, CancellationToken cancellationToken)
	{
		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();
		var reservedDeviceIds = new HashSet<Guid>();
		var driverStates = new Dictionary<string, DriverConfigurationState>();

		foreach (var deviceId in await configurationService.GetDevicesAsync(cancellationToken).ConfigureAwait(false))
		{
			var indexedKey = await configurationService.ReadDeviceConfigurationAsync<IndexedConfigurationKey?>(deviceId, null, cancellationToken).ConfigureAwait(false);
			var config = await configurationService.ReadDeviceConfigurationAsync<DeviceUserConfiguration?>(deviceId, null, cancellationToken).ConfigureAwait(false);
			var info = await configurationService.ReadDeviceConfigurationAsync<DeviceInformation?>(deviceId, null, cancellationToken).ConfigureAwait(false);

			if (indexedKey is null || info is null)
			{
				// TODO: Log an error.
				continue;
			}

			if (config is null)
			{
				// TODO: Log an error.
				config = new() { FriendlyName = info.FriendlyName };
			}

			var key = new DeviceConfigurationKey(indexedKey.DriverKey, indexedKey.DeviceMainId, indexedKey.CompatibleHardwareId, indexedKey.SerialNumber);

			deviceStates.TryAdd(deviceId, new(deviceId, indexedKey, info, config));
			reservedDeviceIds.Add(deviceId);

			// Validating the configuration by registering all the keys properly so that we have bidirectional mappings.
			// For now, we will make the service crash if any invalid configuration is found, but we could also choose to archive the conflicting configurations somewhere before deleting them.

			if (!driverStates.TryGetValue(indexedKey.DriverKey, out var driverState))
			{
				driverStates.Add(indexedKey.DriverKey, driverState = new(indexedKey.DriverKey));
			}

			driverState.RegisterDevice(deviceId, key);
		}

		return new(configurationService, deviceStates, driverStates, reservedDeviceIds);
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates = new();

	// This is the configuration mapping state.
	// For each driver key, this allows mapping the configuration key to a unique device ID that will serve as the configuration ID.
	private readonly Dictionary<string, DriverConfigurationState> _driverConfigurationStates = new();

	// We need to maintain a strict list of already used device IDs just to be absolutely certain to not reuse a device ID during the run of the service.
	// It is a very niche scenario, but newly created devices could reuse the device IDs of a recently removed device and generate a mix-up between concurrently running change listeners,
	// as async code does not have a guaranteed order of execution. This is a problem for all dependent services such as the lighting service or the settings UI itself.
	// The contents of this blacklist do not strictly need to be persisted between service runs, but it could be useful to keep it around to avoid even more niche scenarios.
	private readonly HashSet<Guid> _reservedDeviceIds = new();

	private readonly AsyncLock _lock;

	private ChannelWriter<(UpdateKind, DeviceStateInformation, Driver)>[]? _deviceChangeListeners;

	private readonly ConfigurationService _configurationService;

	AsyncLock IInternalDriverRegistry.Lock => _lock;

	private DeviceRegistry
	(
		ConfigurationService configurationService,
		ConcurrentDictionary<Guid, DeviceState> deviceStates,
		Dictionary<string, DriverConfigurationState> driverConfigurationStates,
		HashSet<Guid> reservedDeviceIds
	)
	{
		_configurationService = configurationService;
		_deviceStates = deviceStates;
		_driverConfigurationStates = driverConfigurationStates;
		_reservedDeviceIds = reservedDeviceIds;
		_lock = new();
	}

	public void Dispose() { }

	// This generates a new unique device ID, handling unlikely but possible GUID collisions.
	// This method must be called from within the lock to strictly ensure that new IDs are really unique, and because the blacklist is not concurrent-safe.
	private Guid CreateNewDeviceId()
	{
		while (true)
		{
			var id = Guid.NewGuid();
			if (!_deviceStates.ContainsKey(id) && _reservedDeviceIds.Add(id))
			{
				return id;
			}
		}
	}

	ValueTask<bool> IInternalDriverRegistry.AddDriverAsync(Driver driver) => AddDriverInLockAsync(driver);
	ValueTask<bool> IInternalDriverRegistry.RemoveDriverAsync(Driver driver) => RemoveDriverInLock(driver);

	public NestedDriverRegistry CreateNestedRegistry() => new(this);
	IDriverRegistry IDriverRegistry.CreateNestedRegistry() => CreateNestedRegistry();

	public async ValueTask<bool> AddDriverAsync(Driver driver)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			return await AddDriverInLockAsync(driver).ConfigureAwait(false);
		}
	}

	private async ValueTask<bool> AddDriverInLockAsync(Driver driver)
	{
		bool isNewDevice = false;

		var key = driver.ConfigurationKey;

		if (!_driverConfigurationStates.TryGetValue(key.DriverKey, out var driverConfigurationState))
		{
			_driverConfigurationStates.Add(key.DriverKey, driverConfigurationState = new(key.DriverKey));
		}

		var featureIds = new HashSet<Guid>(GetDriverFeatures(driver.GetType()).Select(TypeId.Get));
		var (deviceIds, mainDeviceIdIndex) = GetDeviceIds(driver);
		string? serialNumber = GetSerialNumber(driver);
		DeviceState? deviceState;
		IndexedConfigurationKey indexedConfigurationKey;

		if (!driverConfigurationState.TryGetDeviceId(key, out var deviceId))
		{
			deviceId = CreateNewDeviceId();
			indexedConfigurationKey = driverConfigurationState.RegisterDevice(deviceId, key);
			goto CreateState;
		}
		else if (_deviceStates.TryGetValue(deviceId, out deviceState))
		{
			var oldKey = (DeviceConfigurationKey)deviceState.ConfigurationKey;
			if (oldKey != key)
			{
				indexedConfigurationKey = driverConfigurationState.UpdateDeviceConfigurationKey(deviceId, oldKey, key);
				deviceState.ConfigurationKey = indexedConfigurationKey;
				await _configurationService.WriteDeviceConfigurationAsync(deviceId, indexedConfigurationKey, default).ConfigureAwait(false);
			}

			// Avoid creating a new device information instance if the data has not changed.
			if (deviceState.DeviceInformation.FriendlyName != driver.FriendlyName ||
				deviceState.DeviceInformation.Category != driver.DeviceCategory ||
				!deviceState.DeviceInformation.FeatureIds.SequenceEqual(featureIds) ||
				!deviceState.DeviceInformation.DeviceIds.SequenceEqual(deviceIds) ||
				deviceState.DeviceInformation.MainDeviceIdIndex != mainDeviceIdIndex ||
				deviceState.DeviceInformation.SerialNumber != serialNumber)
			{
				deviceState.DeviceInformation = new(driver.FriendlyName, driver.DeviceCategory, featureIds, deviceIds, serialNumber);
				await _configurationService.WriteDeviceConfigurationAsync(deviceId, deviceState.DeviceInformation, default).ConfigureAwait(false);
			}

			goto StateUpdated;
		}
		else
		{
			// TODO: Log warning about missing device state. (If we know the ID, the state must exist)
			indexedConfigurationKey = driverConfigurationState.GetIndexedKey(deviceId, key);
		}
	CreateState:;
		if (!_deviceStates.TryAdd(deviceId, deviceState = new(deviceId, indexedConfigurationKey, new DeviceInformation(driver.FriendlyName, driver.DeviceCategory, featureIds, deviceIds, serialNumber), new() { FriendlyName = driver.FriendlyName })))
		{
			throw new InvalidOperationException();
		}
		isNewDevice = true;

		await _configurationService.WriteDeviceConfigurationAsync(deviceId, indexedConfigurationKey, default).ConfigureAwait(false);
		await _configurationService.WriteDeviceConfigurationAsync(deviceId, deviceState.DeviceInformation, default).ConfigureAwait(false);

	StateUpdated:;
		if (deviceState.TrySetDriver(driver))
		{
			if (_deviceChangeListeners is { } dcl)
			{
				dcl.TryWrite((isNewDevice ? UpdateKind.AddedDevice : UpdateKind.ConnectedDevice, deviceState.GetDeviceStateInformation(), driver));
			}
			return true;
		}
		else
		{
			return false;
		}
	}

	private static (ImmutableArray<DeviceId>, int?) GetDeviceIds(Driver driver)
	{
		if (driver.Features.GetFeature<IDeviceIdsFeature>() is { } deviceIdsFeature)
		{
			return (deviceIdsFeature.DeviceIds, deviceIdsFeature.MainDeviceIdIndex);
		}
		else if (driver.Features.GetFeature<IDeviceIdFeature>() is { } deviceIdFeature)
		{
			return (ImmutableArray.Create(deviceIdFeature.DeviceId), 0);
		}
		return (ImmutableArray<DeviceId>.Empty, null);
	}

	private string? GetSerialNumber(Driver driver)
		=> driver.Features.GetFeature<ISerialNumberDeviceFeature>()?.SerialNumber;

	public async ValueTask<bool> RemoveDriverAsync(Driver driver)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			return await RemoveDriverInLock(driver).ConfigureAwait(false);
		}
	}

	internal ValueTask<bool> RemoveDriverInLock(Driver driver)
	{
		if (TryGetDeviceId(driver, out var deviceId) && _deviceStates.TryGetValue(deviceId, out var deviceState) && deviceState.TryUnsetDriver())
		{
			try
			{
				if (_deviceChangeListeners is { } dcl)
				{
					dcl.TryWrite((UpdateKind.DisconnectedDevice, deviceState.GetDeviceStateInformation(), driver));
				}
			}
			catch (AggregateException)
			{
				// TODO: Log
			}
			return new(true);
		}
		else
		{
			return new(false);
		}
	}

	/// <summary>Watch for all known devices.</summary>
	/// <remarks>
	/// <para>
	/// Unlike <see cref="WatchAvailableAsync(CancellationToken)"/>, this will watch changes to known devices. Devices being made available or unavailable will show up as update notifications.
	/// Other than the few occasional additions du to connecting new devices, the list of known devices should remain mostly static.
	/// The status of those devices should however change as devices are connected or disconnected.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<(UpdateKind Kind, DeviceStateInformation deviceInformation, Driver Driver)>();

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceStates.Count, 10));
			foreach (var state in _deviceStates.Values)
			{
				initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver);
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				for (int i = 0; i < initialNotificationCount; i++)
				{
					yield return initialNotifications[i];
				}
			}
			finally
			{
				ArrayPool<DeviceWatchNotification>.Shared.Return(initialNotifications, true);
				initialNotifications = null;
			}

			await foreach (var (kind, deviceInformation, driver) in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return new
				(
					kind switch 
					{
						UpdateKind.AddedDevice => WatchNotificationKind.Addition,
						UpdateKind.RemovedDevice => WatchNotificationKind.Removal,
						_ => WatchNotificationKind.Update
					},
					deviceInformation,
					driver
				);
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}
	}

	/// <summary>Watch for available devices.</summary>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAvailableAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<(UpdateKind Kind, DeviceStateInformation deviceInformation, Driver Driver)>();

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceStates.Count, 10));
			foreach (var state in _deviceStates.Values)
			{
				if (state.Driver is not null)
				{
					initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver);
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				for (int i = 0; i < initialNotificationCount; i++)
				{
					yield return initialNotifications[i];
				}
			}
			finally
			{
				ArrayPool<DeviceWatchNotification>.Shared.Return(initialNotifications, true);
				initialNotifications = null;
			}

			await foreach (var (kind, deviceInformation, driver) in channel.Reader.ReadAllAsync(cancellationToken))
			{
				// Removal notifications can only happen for disconnected devices, so they should be ignored.
				if (kind != UpdateKind.RemovedDevice)
				{
					yield return new
					(
						kind switch
						{
							UpdateKind.AddedDevice or UpdateKind.ConnectedDevice => WatchNotificationKind.Addition,
							UpdateKind.DisconnectedDevice => WatchNotificationKind.Removal,
							_ => WatchNotificationKind.Update
						},
						deviceInformation,
						driver
					);
				}
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}
	}

	/// <summary>Watch for all known devices providing the specified feature set.</summary>
	/// <remarks>
	/// Just because a driver implements <see cref="IDeviceDriver{TFeature}"/> does not necessarily means that the instance actually exposes any such feature.
	/// Drivers should generally not expose feature sets that they do not support, but some specific devices may end up having no corresponding features.
	/// Such device drivers will still be reported by this method. It is up to the consumer of the notifications to decide what to do with the devices.
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAllAsync<TFeature>([EnumeratorCancellation] CancellationToken cancellationToken)
		where TFeature : class, IDeviceFeature
	{
		var channel = Watcher.CreateChannel<(UpdateKind Kind, DeviceStateInformation deviceInformation, Driver Driver)>();

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;
		// We need to be aware of the feature ID for when the devices are disconnected.
		var featureId = TypeId.Get<TFeature>();

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceStates.Count, 10));
			foreach (var state in _deviceStates.Values)
			{
				if (state.Driver is null ? state.DeviceInformation.FeatureIds.Contains(featureId) : state.Driver is IDeviceDriver<TFeature>)
				{
					initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver);
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				for (int i = 0; i < initialNotificationCount; i++)
				{
					yield return initialNotifications[i];
				}
			}
			finally
			{
				ArrayPool<DeviceWatchNotification>.Shared.Return(initialNotifications, true);
				initialNotifications = null;
			}

			await foreach (var (kind, deviceInformation, driver) in channel.Reader.ReadAllAsync(cancellationToken))
			{
				if (driver is null ? deviceInformation.FeatureIds.Contains(featureId) : driver is IDeviceDriver<TFeature>)
				{
					yield return new
					(
						kind switch
						{
							UpdateKind.AddedDevice => WatchNotificationKind.Addition,
							UpdateKind.RemovedDevice => WatchNotificationKind.Removal,
							_ => WatchNotificationKind.Update
						},
						deviceInformation,
						driver
					);
				}
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}
	}

	/// <summary>Watch for available devices providing the specified feature set.</summary>
	/// <remarks>
	/// Just because a driver implements <see cref="IDeviceDriver{TFeature}"/> does not necessarily means that the instance actually exposes any such feature.
	/// Drivers should generally not expose feature sets that they do not support, but some specific devices may end up having no corresponding features.
	/// Such device drivers will still be reported by this method. It is up to the consumer of the notifications to decide what to do with the devices.
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAvailableAsync<TFeature>([EnumeratorCancellation] CancellationToken cancellationToken)
		where TFeature : class, IDeviceFeature
	{
		var channel = Watcher.CreateChannel<(UpdateKind Kind, DeviceStateInformation deviceInformation, Driver Driver)>();

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceStates.Count, 10));
			foreach (var state in _deviceStates.Values)
			{
				if (state.Driver is IDeviceDriver<TFeature>)
				{
					initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver);
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				for (int i = 0; i < initialNotificationCount; i++)
				{
					yield return initialNotifications[i];
				}
			}
			finally
			{
				ArrayPool<DeviceWatchNotification>.Shared.Return(initialNotifications, true);
				initialNotifications = null;
			}

			await foreach (var (kind, deviceInformation, driver) in channel.Reader.ReadAllAsync(cancellationToken))
			{
				// Removal notifications can only happen for disconnected devices, so they should be ignored.
				if (kind != UpdateKind.RemovedDevice)
				{
					if (driver is IDeviceDriver<TFeature>)
					{
						yield return new
						(
							kind switch
							{
								UpdateKind.AddedDevice or UpdateKind.ConnectedDevice => WatchNotificationKind.Addition,
								UpdateKind.DisconnectedDevice => WatchNotificationKind.Removal,
								_ => WatchNotificationKind.Update
							},
							deviceInformation,
							driver
						);
					}
				}
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceChangeListeners, channel);
		}
	}

	public bool TryGetDriver(Guid deviceId, [NotNullWhen(true)] out Driver? driver)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			return (driver = deviceState.Driver) is not null;
		}
		else
		{
			driver = null;
			return false;
		}
	}

	public bool TryGetDeviceName(Guid deviceId, [NotNullWhen(true)] out string? deviceName)
	{
		if (_deviceStates.TryGetValue(deviceId, out var deviceState))
		{
			deviceName = deviceState.UserConfiguration.FriendlyName;
			return true;
		}
		else
		{
			deviceName = null;
			return false;
		}
	}

	public bool TryGetDeviceId(Driver driver, [NotNullWhen(true)] out Guid deviceId)
		=> TryGetDeviceId(driver.ConfigurationKey, out deviceId);

	private bool TryGetDeviceId(DeviceConfigurationKey key, [NotNullWhen(true)] out Guid deviceId)
	{
		if (_driverConfigurationStates.TryGetValue(key.DriverKey, out var driverConfigurationState))
		{
			return driverConfigurationState.TryGetDeviceId(key, out deviceId);
		}
		else
		{
			deviceId = default;
			return false;
		}
	}
}
