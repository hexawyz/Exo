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
			: this(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId, instanceIndex) { }

		public IndexedConfigurationKey(IndexedConfigurationKey key, int instanceIndex)
			: this(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId, instanceIndex) { }

		[JsonConstructor]
		public IndexedConfigurationKey(string driverKey, string deviceMainId, string compatibleHardwareId, string? uniqueId, int instanceIndex)
		{
			DriverKey = driverKey;
			DeviceMainId = deviceMainId;
			CompatibleHardwareId = compatibleHardwareId;
			UniqueId = uniqueId;
			InstanceIndex = instanceIndex;
		}

		public string DriverKey { get; }
		public string DeviceMainId { get; }
		public string CompatibleHardwareId { get; }
		public string? UniqueId { get; }
		public int InstanceIndex { get; }

		public static explicit operator DeviceConfigurationKey(IndexedConfigurationKey key)
			=> new(key.DriverKey, key.DeviceMainId, key.CompatibleHardwareId, key.UniqueId);
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
		public Dictionary<string, DeviceState> DevicesByUniqueId { get; } = new();
		public Dictionary<string, DeviceState> DevicesByMainDeviceName { get; } = new();
		public Dictionary<string, List<DeviceState>> DevicesByCompatibleHardwareId { get; } = new();

		public DriverConfigurationState(string key) => Key = key;

		// TODO: Move the device management methods back into the parent class. Because it mixes up with writing the configuration, it is not the right place to have this code. (but it is also not urgent)
		// We only need this class as an implementation detail to hold related state information together anyway. It is fine if everything is accessed from outside.

		
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

			// Validating the configuration by registering all the keys properly so that we have bidirectional mappings.
			// For now, we will make the service crash if any invalid configuration is found, but we could also choose to archive the conflicting configurations somewhere before deleting them.

			if (!driverStates.TryGetValue(indexedKey.DriverKey, out var driverState))
			{
				driverStates.Add(indexedKey.DriverKey, driverState = new(indexedKey.DriverKey));
			}

			deviceStates.TryAdd(deviceId, RegisterDevice(driverState, deviceId, indexedKey, info));
			reservedDeviceIds.Add(deviceId);
		}

		foreach (var driverState in driverStates.Values)
		{
			await FixupDeviceInstanceIdsAsync(configurationService, driverState.DevicesByCompatibleHardwareId).ConfigureAwait(false);
		}

		return new(configurationService, deviceStates, driverStates, reservedDeviceIds);
	}

	// This supports upgrading from no serial number to serial number for now, but it is maybe not not a desirable feature in the long run.
	private static bool TryGetDevice(DriverConfigurationState state, in DeviceConfigurationKey key, [NotNullWhen(true)] out DeviceState? device)
	{
		// This helper method is for the fallback to main device ID case from the unique ID.
		// It ensures that we avoid leaking the device object in case there is a match but with a unique ID. (Which we assume to be different)
		static bool TryGetDeviceByMainDeviceNameFallback(DriverConfigurationState state, in DeviceConfigurationKey key, [NotNullWhen(true)] out DeviceState? device)
		{
			if (state.DevicesByMainDeviceName.TryGetValue(key.DeviceMainId, out device))
			{
				if (device.ConfigurationKey.UniqueId is null) return true;
				device = null;
			}
			return false;
		}

		return key.UniqueId is not null ?
			state.DevicesByUniqueId.TryGetValue(key.UniqueId, out device) || TryGetDeviceByMainDeviceNameFallback(state, key, out device):
			state.DevicesByMainDeviceName.TryGetValue(key.DeviceMainId, out device);
	}

	private static List<DeviceState> GetDevicesWithCompatibleHardwareId(Dictionary<string, List<DeviceState>> devicesByCompatibleHardwareId, string compatibleHardwareId)
	{
		if (!devicesByCompatibleHardwareId.TryGetValue(compatibleHardwareId, out var devicesWithCompatibleHardwareId))
		{
			devicesByCompatibleHardwareId.TryAdd(compatibleHardwareId, devicesWithCompatibleHardwareId = new());
		}

		return devicesWithCompatibleHardwareId;
	}

	// To be called only during initialization. Here we recover an existing configuration that was persisted.
	// This can be done out of order, and instance indices will be fixed later.
	private static DeviceState RegisterDevice(DriverConfigurationState state, in Guid deviceId, IndexedConfigurationKey key, DeviceInformation deviceInformation)
		=> RegisterDeviceInternal(state, deviceId, key, deviceInformation, GetDevicesWithCompatibleHardwareId(state.DevicesByCompatibleHardwareId, key.CompatibleHardwareId));

	// To be called during runtime when a new device is registered. A proper device instance index will be allocated.
	private static DeviceState RegisterNewDevice(DriverConfigurationState state, in Guid deviceId, in DeviceConfigurationKey key, DeviceInformation deviceInformation)
	{
		var devicesWithCompatibleHardwareId = GetDevicesWithCompatibleHardwareId(state.DevicesByCompatibleHardwareId, key.CompatibleHardwareId);

		return RegisterDeviceInternal(state, deviceId, new(key, devicesWithCompatibleHardwareId.Count), deviceInformation, devicesWithCompatibleHardwareId);
	}

	private static DeviceState RegisterDeviceInternal
	(
		DriverConfigurationState state,
		in Guid deviceId,
		in IndexedConfigurationKey key,
		DeviceInformation deviceInformation,
		List<DeviceState> devicesWithCompatibleHardwareId
	)
	{
		var deviceState = new DeviceState
		(
			deviceId,
			key,
			deviceInformation,
			new DeviceUserConfiguration { FriendlyName = deviceInformation.FriendlyName, IsAutomaticallyRemapped = false }
		);

		devicesWithCompatibleHardwareId.Add(deviceState);

		if (key.UniqueId is not null)
		{
			if (!state.DevicesByUniqueId.TryAdd(key.UniqueId, deviceState))
			{
				state.DevicesByUniqueId.TryGetValue(key.UniqueId, out var otherDeviceId);
				throw new InvalidOperationException($"A non unique serial number was provided for devices {otherDeviceId:B} and {deviceId:B}.");
			}
		}

		if (!state.DevicesByMainDeviceName.TryAdd(key.DeviceMainId, deviceState))
		{
			state.DevicesByMainDeviceName.TryGetValue(key.DeviceMainId, out var otherDeviceId);
			throw new InvalidOperationException($"The same main device name was found for devices {otherDeviceId:B} and {deviceId:B}.");
		}

		return deviceState;
	}

	// Only to be called during initialization, but necessary to ensure consistency of device instance IDs.
	// This will sort the runtime devices by ID and override all the saved configurations as required.
	private static async ValueTask FixupDeviceInstanceIdsAsync(ConfigurationService configurationService, Dictionary<string, List<DeviceState>> devicesByCompatibleHardwareId)
	{
		foreach (var devicesWithSameHardwareId in devicesByCompatibleHardwareId.Values)
		{
			devicesWithSameHardwareId.Sort((x, y) => Comparer<int>.Default.Compare(x.ConfigurationKey.InstanceIndex, y.ConfigurationKey.InstanceIndex));
			for (int i = 0; i < devicesWithSameHardwareId.Count; i++)
			{
				var device = devicesWithSameHardwareId[i];
				if (device.ConfigurationKey.InstanceIndex != i)
				{
					device.ConfigurationKey = new(device.ConfigurationKey, i);
					await configurationService.WriteDeviceConfigurationAsync(device.DeviceId, device.ConfigurationKey, default).ConfigureAwait(false);
				}
			}
		}
	}

	private static async ValueTask UpdateDeviceConfigurationKeyAsync(ConfigurationService configurationService, DriverConfigurationState state, DeviceState device, DeviceConfigurationKey newKey)
	{
		var oldKey = device.ConfigurationKey;
		int instanceIndex = device.ConfigurationKey.InstanceIndex;
		bool hasChanged = false;
		bool shouldUpdateCompatibleDevices = false;

		if (oldKey.UniqueId != newKey.UniqueId)
		{
			if (oldKey.UniqueId is not null)
			{
				state.DevicesByUniqueId.Remove(oldKey.UniqueId);
			}
			if (newKey.UniqueId is not null)
			{
				state.DevicesByUniqueId.Add(newKey.UniqueId, device);
			}
			hasChanged = true;
		}
		if (oldKey.DeviceMainId != newKey.DeviceMainId)
		{
			state.DevicesByMainDeviceName.Remove(oldKey.DeviceMainId);
			state.DevicesByMainDeviceName.Add(newKey.DeviceMainId, device);
			hasChanged = true;
		}
		// The old index will be unallocated afterwards, as allocating the newer index is the most important operation.
		// In case the service is interrupted after this new configuration is saved, it will always allow recovering the proper state on next startup.
		// The update of old indices is still required for correct runtime state, but it can be recomputed easily from the saved configuration.
		if (oldKey.CompatibleHardwareId != newKey.CompatibleHardwareId)
		{
			if (!state.DevicesByCompatibleHardwareId.TryGetValue(newKey.CompatibleHardwareId, out var devicesWithSameHardwareId))
			{
				state.DevicesByCompatibleHardwareId.Add(newKey.CompatibleHardwareId, devicesWithSameHardwareId = new());
			}
			instanceIndex = devicesWithSameHardwareId.Count;
			devicesWithSameHardwareId.Add(device);
			shouldUpdateCompatibleDevices = true;
			hasChanged = true;
		}
		if (hasChanged)
		{
			device.ConfigurationKey = new(newKey, instanceIndex);
			await configurationService.WriteDeviceConfigurationAsync(device.DeviceId, device.ConfigurationKey, default).ConfigureAwait(false);

			if (shouldUpdateCompatibleDevices)
			{
				if (state.DevicesByCompatibleHardwareId.TryGetValue(oldKey.CompatibleHardwareId, out var devicesWithSameHardwareId) && devicesWithSameHardwareId.IndexOf(device) is int index and >= 0)
				{
					devicesWithSameHardwareId.RemoveAt(index);
					if (devicesWithSameHardwareId.Count == 0)
					{
						state.DevicesByCompatibleHardwareId.Remove(oldKey.CompatibleHardwareId);
					}
					else
					{
						for (int i = index; i < devicesWithSameHardwareId.Count; i++)
						{
							var otherDevice = devicesWithSameHardwareId[i];
							otherDevice.ConfigurationKey = new(otherDevice.ConfigurationKey, i);

							await configurationService.WriteDeviceConfigurationAsync(otherDevice.DeviceId, otherDevice.ConfigurationKey, default).ConfigureAwait(false);
						}
					}
				}
			}
		}
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
	IDriverRegistry INestedDriverRegistryProvider.CreateNestedRegistry() => CreateNestedRegistry();

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

		if (TryGetDevice(driverConfigurationState, key, out deviceState))
		{
			await UpdateDeviceConfigurationKeyAsync(_configurationService, driverConfigurationState, deviceState, key).ConfigureAwait(false);

			// Avoid creating a new device information instance if the data has not changed.
			if (deviceState.DeviceInformation.FriendlyName != driver.FriendlyName ||
				deviceState.DeviceInformation.Category != driver.DeviceCategory ||
				!deviceState.DeviceInformation.FeatureIds.SequenceEqual(featureIds) ||
				!deviceState.DeviceInformation.DeviceIds.SequenceEqual(deviceIds) ||
				deviceState.DeviceInformation.MainDeviceIdIndex != mainDeviceIdIndex ||
				deviceState.DeviceInformation.SerialNumber != serialNumber)
			{
				deviceState.DeviceInformation = new(driver.FriendlyName, driver.DeviceCategory, featureIds, deviceIds, mainDeviceIdIndex, serialNumber);
				await _configurationService.WriteDeviceConfigurationAsync(deviceState.DeviceId, deviceState.DeviceInformation, default).ConfigureAwait(false);
			}
		}
		else
		{
			var deviceId = CreateNewDeviceId();
			deviceState = RegisterNewDevice(driverConfigurationState, deviceId, key, new DeviceInformation(driver.FriendlyName, driver.DeviceCategory, featureIds, deviceIds, mainDeviceIdIndex, serialNumber));
			if (!_deviceStates.TryAdd(deviceState.DeviceId, deviceState))
			{
				throw new InvalidOperationException();
			}
			await _configurationService.WriteDeviceConfigurationAsync(deviceState.DeviceId, deviceState.ConfigurationKey, default).ConfigureAwait(false);
			await _configurationService.WriteDeviceConfigurationAsync(deviceState.DeviceId, deviceState.DeviceInformation, default).ConfigureAwait(false);
			isNewDevice = true;
		}

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
		=> driver.Features.GetFeature<IDeviceSerialNumberFeature>()?.SerialNumber;

	public async ValueTask<bool> RemoveDriverAsync(Driver driver)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			return await RemoveDriverInLock(driver).ConfigureAwait(false);
		}
	}

	internal ValueTask<bool> RemoveDriverInLock(Driver driver)
	{
		if (TryGetDevice(driver, out var device) && device.TryUnsetDriver())
		{
			try
			{
				if (_deviceChangeListeners is { } dcl)
				{
					dcl.TryWrite((UpdateKind.DisconnectedDevice, device.GetDeviceStateInformation(), driver));
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
		if (_driverConfigurationStates.TryGetValue(key.DriverKey, out var driverConfigurationState) && TryGetDevice(driverConfigurationState, key, out var device))
		{
			deviceId = device.DeviceId;
			return true;
		}
		else
		{
			deviceId = default;
			return false;
		}
	}

	private bool TryGetDevice(Driver driver, [NotNullWhen(true)] out DeviceState? device)
		=> TryGetDevice(driver.ConfigurationKey, out device);

	private bool TryGetDevice(DeviceConfigurationKey key, [NotNullWhen(true)] out DeviceState? device)
	{
		if (_driverConfigurationStates.TryGetValue(key.DriverKey, out var driverConfigurationState))
		{
			return TryGetDevice(driverConfigurationState, key, out device);
		}
		else
		{
			device = default;
			return false;
		}
	}
}
