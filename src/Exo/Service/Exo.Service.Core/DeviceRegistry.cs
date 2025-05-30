using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DeviceTools;
using Exo.Configuration;
using Exo.Features;
using Exo.Service.Configuration;
using Microsoft.Extensions.Logging;

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
		UpdatedSupportedFeatures = 5,
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
				DeviceInformation.SupportedFeatureIds,
				DeviceInformation.DeviceIds,
				DeviceInformation.MainDeviceIdIndex,
				DeviceInformation.SerialNumber,
				_driver is not null
			);
	}

	private sealed class DriverConfigurationState
	{
		public string Key { get; }
		public Dictionary<string, DeviceState> DevicesByUniqueId { get; } = [];
		// We will support multiple different devices being mapped to the same main device name for now.
		// This is one way of solving the problem of connecting to many monitors of the same model but with a different S/N.
		// In some cases we would want to ignore the serial number, but it is unclear how to handle this now, as we still want to track a device moving ports.
		public Dictionary<string, object> DevicesByMainDeviceName { get; } = [];
		public Dictionary<string, List<DeviceState>> DevicesByCompatibleHardwareId { get; } = [];

		public DriverConfigurationState(string key) => Key = key;
	}

	private readonly struct NotificationDetails
	{
		public required UpdateKind Kind { get; init; }
		public required DeviceStateInformation DeviceInformation { get; init; }
		public Driver? Driver { get; init; }
		public IDeviceFeatureSet? FeatureSet { get; init; }
	}

	public static async Task<DeviceRegistry> CreateAsync(ILogger<DeviceRegistry> logger, IConfigurationContainer<Guid> deviceConfigurationService, CancellationToken cancellationToken)
	{
		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();
		var reservedDeviceIds = new HashSet<Guid>();
		var driverStates = new Dictionary<string, DriverConfigurationState>();

		foreach (var deviceId in await deviceConfigurationService.GetKeysAsync(cancellationToken).ConfigureAwait(false))
		{
			var indexedKeyResult = await deviceConfigurationService.ReadValueAsync(deviceId, SourceGenerationContext.Default.IndexedConfigurationKey, cancellationToken).ConfigureAwait(false);
			var configResult = await deviceConfigurationService.ReadValueAsync(deviceId, SourceGenerationContext.Default.DeviceUserConfiguration, cancellationToken).ConfigureAwait(false);
			var infoResult = await deviceConfigurationService.ReadValueAsync(deviceId, SourceGenerationContext.Default.DeviceInformation, cancellationToken).ConfigureAwait(false);

			if (!indexedKeyResult.Found || !infoResult.Found)
			{
				// TODO: Log an error.
				continue;
			}

			var indexedKey = indexedKeyResult.Value!;
			var info = infoResult.Value!;

			DeviceUserConfiguration config;
			if (configResult.Found)
			{
				config = configResult.Value!;
			}
			else
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
			await FixupDeviceInstanceIdsAsync(deviceConfigurationService, driverState.DevicesByCompatibleHardwareId).ConfigureAwait(false);
		}

		return new(logger, deviceConfigurationService, deviceStates, driverStates, reservedDeviceIds);
	}

	// This supports upgrading from no serial number to serial number for now, but it is maybe not not a desirable feature in the long run.
	private static bool TryGetDevice(DriverConfigurationState state, in DeviceConfigurationKey key, [NotNullWhen(true)] out DeviceState? device)
	{
		// This helper method tries to find a single device without unique ID.
		// It can be used to find a device without unique ID or to upgrade a device state without unique ID to one with a device ID.
		static bool TryGetDeviceByMainDeviceName(DriverConfigurationState state, in DeviceConfigurationKey key, [NotNullWhen(true)] out DeviceState? device)
		{
			if (state.DevicesByMainDeviceName.TryGetValue(key.DeviceMainId, out var devices))
			{
				if (devices is DeviceState { ConfigurationKey.UniqueId: null } foundDevice)
				{
					device = foundDevice;
					return true;
				}
			}
			device = null;
			return false;
		}

		return key.UniqueId is not null ?
			state.DevicesByUniqueId.TryGetValue(key.UniqueId, out device) || TryGetDeviceByMainDeviceName(state, key, out device) :
			TryGetDeviceByMainDeviceName(state, key, out device);
	}

	private static List<DeviceState> GetDevicesWithCompatibleHardwareId(Dictionary<string, List<DeviceState>> devicesByCompatibleHardwareId, string compatibleHardwareId)
	{
		if (!devicesByCompatibleHardwareId.TryGetValue(compatibleHardwareId, out var devicesWithCompatibleHardwareId))
		{
			devicesByCompatibleHardwareId.TryAdd(compatibleHardwareId, devicesWithCompatibleHardwareId = []);
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

		RegisterMainDeviceId(state, deviceState);

		return deviceState;
	}

	private static void RegisterMainDeviceId(DriverConfigurationState state, DeviceState device)
		=> RegisterMainDeviceId(state, device, device.ConfigurationKey.DeviceMainId, device.ConfigurationKey.UniqueId);

	private static void RegisterMainDeviceId(DriverConfigurationState state, DeviceState device, string mainDeviceId, string? uniqueId)
	{
		// The logic for main ID is as follows:
		// We allow multiple devices with a unique ID to be associated with the same main device name,
		// but a device without a unique ID must be exclusively associated with a main device name.
		if (!state.DevicesByMainDeviceName.TryAdd(mainDeviceId, device))
		{
			if (!state.DevicesByMainDeviceName.TryGetValue(mainDeviceId, out var currentState))
				throw new UnreachableException("Main device name conflict.");

			// A device without unique ID must have exclusive ID.
			if (uniqueId is null)
			{
				throw new InvalidOperationException($"There is a main device name conflict for device {device.DeviceId:B} without unique ID.");
			}
			if (currentState is DeviceState otherDeviceState)
			{
				if (otherDeviceState.ConfigurationKey.UniqueId is null)
				{
					throw new InvalidOperationException($"There is a main device name associated to device {otherDeviceState.DeviceId:B} without device ID cannot be reused by device {device.DeviceId:B}.");
				}

				// If we have ensured that both devices have a unique ID, we can simply put the two devices into a list.
				state.DevicesByMainDeviceName[mainDeviceId] = new List<DeviceState> { otherDeviceState, device };
			}
			else
			{
				// The last remaining case is that the object stored was already a list containing devices with a unique ID, so we can just add another one here.
				((List<DeviceState>)currentState).Add(device);
			}
		}
	}

	private static void UnregisterMainDeviceId(DriverConfigurationState state, DeviceState device, string mainDeviceId)
	{
		static int FindIndex(List<DeviceState>? devices, Guid deviceId)
		{
			if (devices is not null)
			{
				var span = CollectionsMarshal.AsSpan(devices);
				for (int i = 0; i < span.Length; i++)
				{
					if (span[i].DeviceId == deviceId) return i;
				}
			}
			return -1;
		}

		if (!((IDictionary<string, object>)state.DevicesByMainDeviceName).Remove(new KeyValuePair<string, object>(mainDeviceId, device)))
		{
			if (!state.DevicesByMainDeviceName.TryGetValue(mainDeviceId, out var currentState) ||
				currentState is not List<DeviceState> devices ||
				FindIndex(devices, device.DeviceId) is int index && index < 0)
				throw new UnreachableException("Main device name removal.");

			devices.RemoveAt(index);

			// If there is only one device left, revert to having it directly referenced instead of a list of devices.
			if (devices.Count == 1)
			{
				state.DevicesByMainDeviceName[mainDeviceId] = devices[0];
			}
		}
	}

	// Only to be called during initialization, but necessary to ensure consistency of device instance IDs.
	// This will sort the runtime devices by ID and override all the saved configurations as required.
	private static async ValueTask FixupDeviceInstanceIdsAsync(IConfigurationContainer<Guid> deviceConfigurationService, Dictionary<string, List<DeviceState>> devicesByCompatibleHardwareId)
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
					await deviceConfigurationService.WriteValueAsync(device.DeviceId, device.ConfigurationKey, SourceGenerationContext.Default.IndexedConfigurationKey, default).ConfigureAwait(false);
				}
			}
		}
	}

	private static async ValueTask UpdateDeviceConfigurationKeyAsync(IConfigurationContainer<Guid> deviceConfigurationService, DriverConfigurationState state, DeviceState device, DeviceConfigurationKey newKey)
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
			UnregisterMainDeviceId(state, device, oldKey.DeviceMainId);
			RegisterMainDeviceId(state, device, newKey.DeviceMainId, newKey.UniqueId);
			hasChanged = true;
		}
		// The old index will be unallocated afterwards, as allocating the newer index is the most important operation.
		// In case the service is interrupted after this new configuration is saved, it will always allow recovering the proper state on next startup.
		// The update of old indices is still required for correct runtime state, but it can be recomputed easily from the saved configuration.
		if (oldKey.CompatibleHardwareId != newKey.CompatibleHardwareId)
		{
			if (!state.DevicesByCompatibleHardwareId.TryGetValue(newKey.CompatibleHardwareId, out var devicesWithSameHardwareId))
			{
				state.DevicesByCompatibleHardwareId.Add(newKey.CompatibleHardwareId, devicesWithSameHardwareId = []);
			}
			instanceIndex = devicesWithSameHardwareId.Count;
			devicesWithSameHardwareId.Add(device);
			shouldUpdateCompatibleDevices = true;
			hasChanged = true;
		}
		if (hasChanged)
		{
			device.ConfigurationKey = new(newKey, instanceIndex);
			await deviceConfigurationService.WriteValueAsync(device.DeviceId, device.ConfigurationKey, SourceGenerationContext.Default.IndexedConfigurationKey, default).ConfigureAwait(false);

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

							await deviceConfigurationService.WriteValueAsync(otherDevice.DeviceId, otherDevice.ConfigurationKey, SourceGenerationContext.Default.IndexedConfigurationKey, default).ConfigureAwait(false);
						}
					}
				}
			}
		}
	}

	private readonly ConcurrentDictionary<Guid, DeviceState> _deviceStates = new();

	// This is the configuration mapping state.
	// For each driver key, this allows mapping the configuration key to a unique device ID that will serve as the configuration ID.
	private readonly Dictionary<string, DriverConfigurationState> _driverConfigurationStates = [];

	// We need to maintain a strict list of already used device IDs just to be absolutely certain to not reuse a device ID during the run of the service.
	// It is a very niche scenario, but newly created devices could reuse the device IDs of a recently removed device and generate a mix-up between concurrently running change listeners,
	// as async code does not have a guaranteed order of execution. This is a problem for all dependent services such as the lighting service or the settings UI itself.
	// The contents of this blacklist do not strictly need to be persisted between service runs, but it could be useful to keep it around to avoid even more niche scenarios.
	private readonly HashSet<Guid> _reservedDeviceIds = [];

	private readonly AsyncLock _lock;

	private ChannelWriter<NotificationDetails>[]? _deviceChangeListeners;

	private readonly IConfigurationContainer<Guid> _deviceConfigurationService;

	private readonly ILogger<DeviceRegistry> _logger;

	private readonly FeatureSetEventHandler _featureChangeEventHandler;

	AsyncLock IInternalDriverRegistry.Lock => _lock;

	private DeviceRegistry
	(
		ILogger<DeviceRegistry> logger,
		IConfigurationContainer<Guid> deviceConfigurationService,
		ConcurrentDictionary<Guid, DeviceState> deviceStates,
		Dictionary<string, DriverConfigurationState> driverConfigurationStates,
		HashSet<Guid> reservedDeviceIds
	)
	{
		_logger = logger;
		_deviceConfigurationService = deviceConfigurationService;
		_deviceStates = deviceStates;
		_driverConfigurationStates = driverConfigurationStates;
		_reservedDeviceIds = reservedDeviceIds;
		_lock = new();
		_featureChangeEventHandler = new(OnFeatureAvailabilityChange);
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

		var featureIds = new HashSet<Guid>(ImmutableCollectionsMarshal.AsArray(driver.FeatureSets)!.Select(fs => TypeId.Get(fs.FeatureType)));
		var (deviceIds, mainDeviceIdIndex) = GetDeviceIds(driver);
		string? serialNumber = GetSerialNumber(driver);

		if (TryGetDevice(driverConfigurationState, key, out var deviceState))
		{
			await UpdateDeviceConfigurationKeyAsync(_deviceConfigurationService, driverConfigurationState, deviceState, key).ConfigureAwait(false);

			// Avoid creating a new device information instance if the data has not changed.
			if (deviceState.DeviceInformation.FriendlyName != driver.FriendlyName ||
				deviceState.DeviceInformation.Category != driver.DeviceCategory ||
				!deviceState.DeviceInformation.SupportedFeatureIds.SequenceEqual(featureIds) ||
				!deviceState.DeviceInformation.DeviceIds.SequenceEqual(deviceIds) ||
				deviceState.DeviceInformation.MainDeviceIdIndex != mainDeviceIdIndex ||
				deviceState.DeviceInformation.SerialNumber != serialNumber)
			{
				deviceState.DeviceInformation = new(driver.FriendlyName, driver.DeviceCategory, featureIds, deviceIds, mainDeviceIdIndex, serialNumber);
				await _deviceConfigurationService.WriteValueAsync(deviceState.DeviceId, deviceState.DeviceInformation, SourceGenerationContext.Default.DeviceInformation, default).ConfigureAwait(false);
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
			await _deviceConfigurationService.WriteValueAsync(deviceState.DeviceId, deviceState.ConfigurationKey, SourceGenerationContext.Default.IndexedConfigurationKey, default).ConfigureAwait(false);
			await _deviceConfigurationService.WriteValueAsync(deviceState.DeviceId, deviceState.DeviceInformation, SourceGenerationContext.Default.DeviceInformation, default).ConfigureAwait(false);
			isNewDevice = true;
		}

		if (deviceState.TrySetDriver(driver))
		{
			if (driver.GetFeatureSet<IGenericDeviceFeature>().GetFeature<IVariableFeatureSetDeviceFeature>() is { } variableFeatureSetFeature)
			{
				variableFeatureSetFeature.FeatureAvailabilityChanged += _featureChangeEventHandler;
			}
			if (_deviceChangeListeners is { } dcl)
			{
				dcl.TryWrite(new NotificationDetails { Kind = isNewDevice ? UpdateKind.AddedDevice : UpdateKind.ConnectedDevice, DeviceInformation = deviceState.GetDeviceStateInformation(), Driver = driver });
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
		if (driver.GetFeatureSet<IGenericDeviceFeature>() is { IsEmpty: false } genericFeatures)
		{
			if (genericFeatures.GetFeature<IDeviceIdsFeature>() is { } deviceIdsFeature)
			{
				return (deviceIdsFeature.DeviceIds, deviceIdsFeature.MainDeviceIdIndex);
			}
			else if (genericFeatures.GetFeature<IDeviceIdFeature>() is { } deviceIdFeature)
			{
				return (ImmutableArray.Create(deviceIdFeature.DeviceId), 0);
			}
		}
		return ([], null);
	}

	private string? GetSerialNumber(Driver driver)
	{
		try
		{
			return driver.GetFeatureSet<IGenericDeviceFeature>()?.GetFeature<IDeviceSerialNumberFeature>()?.SerialNumber;
		}
		catch (Exception ex)
		{
			_logger.DeviceRegistryDeviceSerialNumberRetrievalFailure(driver.FriendlyName, ex);
			return null;
		}
	}

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
				if (driver.GetFeatureSet<IGenericDeviceFeature>().GetFeature<IVariableFeatureSetDeviceFeature>() is { } variableFeatureSetFeature)
				{
					variableFeatureSetFeature.FeatureAvailabilityChanged -= _featureChangeEventHandler;
				}
			}
			catch (Exception ex)
			{
				// TODO: Log
			}

			try
			{
				if (_deviceChangeListeners is { } dcl)
				{
					dcl.TryWrite(new NotificationDetails { Kind = UpdateKind.DisconnectedDevice, DeviceInformation = device.GetDeviceStateInformation(), Driver = driver });
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

	private async void OnFeatureAvailabilityChange(Driver driver, IDeviceFeatureSet featureSet)
	{
		if (featureSet is null)
		{
			// TODO: Log an error message, as we never ever want featureSet to be null.
			return;
		}

		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			if (TryGetDevice(driver, out var deviceState) && ReferenceEquals(deviceState.Driver, driver) && _deviceChangeListeners is { } dcl)
			{
				dcl.TryWrite
				(
					new NotificationDetails
					{
						Kind = UpdateKind.UpdatedSupportedFeatures,
						DeviceInformation = deviceState.GetDeviceStateInformation(),
						Driver = driver,
						FeatureSet = featureSet,
					}
				);
			}
		}
	}

	/// <summary>Watch for all known devices.</summary>
	/// <remarks>
	/// <para>
	/// Unlike <see cref="WatchAvailableAsync(CancellationToken)"/>, this will watch changes to known devices. Devices being made available or unavailable will show up as update notifications.
	/// Other than the few occasional additions due to connecting new devices, the list of known devices should remain mostly static.
	/// The status of those devices should however change as devices are connected or disconnected.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<NotificationDetails>();

		List<DeviceWatchNotification>? initialNotifications;

		lock (_lock)
		{
			initialNotifications = new(16);
			foreach (var state in _deviceStates.Values)
			{
				initialNotifications.Add(new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver));
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				foreach (var notification in initialNotifications)
				{
					yield return notification;
				}
			}
			finally
			{
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return new
				(
					notification.Kind switch
					{
						UpdateKind.AddedDevice => WatchNotificationKind.Addition,
						UpdateKind.RemovedDevice => WatchNotificationKind.Removal,
						_ => WatchNotificationKind.Update
					},
					notification.DeviceInformation,
					notification.Driver,
					notification.FeatureSet
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
		var channel = Watcher.CreateChannel<NotificationDetails>();

		List<DeviceWatchNotification>? initialNotifications;

		lock (_lock)
		{
			initialNotifications = new(16);
			foreach (var state in _deviceStates.Values)
			{
				if (state.Driver is not null)
				{
					initialNotifications.Add(new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver));
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				foreach (var notification in initialNotifications)
				{
					yield return notification;
				}
			}
			finally
			{
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				// Removal notifications can only happen for disconnected devices, so they should be ignored.
				if (notification.Kind != UpdateKind.RemovedDevice)
				{
					yield return new
					(
						notification.Kind switch
						{
							UpdateKind.AddedDevice or UpdateKind.ConnectedDevice => WatchNotificationKind.Addition,
							UpdateKind.DisconnectedDevice => WatchNotificationKind.Removal,
							_ => WatchNotificationKind.Update
						},
						notification.DeviceInformation,
						notification.Driver,
						notification.FeatureSet
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
		var channel = Watcher.CreateChannel<NotificationDetails>();

		List<DeviceWatchNotification>? initialNotifications;
		// We need to be aware of the feature ID for when the devices are disconnected.
		var featureId = TypeId.Get<TFeature>();

		lock (_lock)
		{
			initialNotifications = new(16);
			foreach (var state in _deviceStates.Values)
			{
				if (state.DeviceInformation.SupportedFeatureIds.Contains(featureId))
				{
					initialNotifications.Add(new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver));
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				foreach (var notification in initialNotifications)
				{
					yield return notification;
				}
			}
			finally
			{
				initialNotifications = null;
			}

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				if (notification.DeviceInformation.FeatureIds.Contains(featureId))
				{
					yield return new
					(
						notification.Kind switch
						{
							UpdateKind.AddedDevice => WatchNotificationKind.Addition,
							UpdateKind.RemovedDevice => WatchNotificationKind.Removal,
							_ => WatchNotificationKind.Update
						},
						notification.DeviceInformation,
						notification.Driver,
						notification.FeatureSet
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
	/// TODO: Make it so that this dynamically reports devices supporting a specific feature set only when it is actually available. It will simplify work in client services by a lot.
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAvailableAsync<TFeature>([EnumeratorCancellation] CancellationToken cancellationToken)
		where TFeature : class, IDeviceFeature
	{
		var channel = Watcher.CreateChannel<NotificationDetails>();

		List<DeviceWatchNotification>? initialNotifications;
		var featureId = TypeId.Get<TFeature>();
		var connectedDeviceIds = new HashSet<Guid>();

		lock (_lock)
		{
			initialNotifications = new(16);
			foreach (var state in _deviceStates.Values)
			{
				if (state.Driver is { } driver && driver.GetFeatureSet<TFeature>() is { IsEmpty: false } featureSet)
				{
					initialNotifications.Add(new(WatchNotificationKind.Enumeration, state.GetDeviceStateInformation(), state.Driver, featureSet));
					connectedDeviceIds.Add(state.DeviceId);
				}
			}

			ArrayExtensions.InterlockedAdd(ref _deviceChangeListeners, channel);
		}

		try
		{
			try
			{
				foreach (var notification in initialNotifications)
				{
					yield return notification;
				}
			}
			finally
			{
				initialNotifications = null;
			}

			// TODO: If we want to support proper in-sequence updates, we should add the changed feature type in the notification parameters.
			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				if (notification.Driver is not null && notification.DeviceInformation.FeatureIds.Contains(featureId))
				{
					// The goal here is to send the feature collection information whenever possible.
					// For updates, there is never a question, as the feature set type and collection are always provided.
					// In other cases, we manually fetch
					WatchNotificationKind notificationKind;
					IDeviceFeatureSet? featureSet = null;
					switch (notification.Kind)
					{
					case UpdateKind.AddedDevice:
					case UpdateKind.ConnectedDevice:
						featureSet = notification.Driver.GetFeatureSet<TFeature>();
						if (featureSet.IsEmpty) continue;
						connectedDeviceIds.Add(notification.DeviceInformation.Id);
						notificationKind = WatchNotificationKind.Addition;
						break;
					case UpdateKind.RemovedDevice:
						// Removal notifications can only happen for disconnected devices, so they should be ignored.
						continue;
					case UpdateKind.DisconnectedDevice:
						featureSet = FeatureSet.Empty<TFeature>();
						connectedDeviceIds.Remove(notification.DeviceInformation.Id);
						notificationKind = WatchNotificationKind.Removal;
						break;
					case UpdateKind.UpdatedSupportedFeatures:
						featureSet = notification.FeatureSet;
						if (featureSet!.FeatureType == typeof(TFeature))
						{
							bool wasConnected = connectedDeviceIds.Contains(notification.DeviceInformation.Id);
							if (wasConnected == featureSet.IsEmpty)
							{
								if (featureSet.IsEmpty)
								{
									connectedDeviceIds.Remove(notification.DeviceInformation.Id);
									notificationKind = WatchNotificationKind.Removal;
								}
								else
								{
									connectedDeviceIds.Add(notification.DeviceInformation.Id);
									notificationKind = WatchNotificationKind.Addition;
								}
								break;
							}
							else if (!wasConnected)
							{
								continue;
							}
							else
							{
								goto default;
							}
						}
						goto default;
					default:
						notificationKind = WatchNotificationKind.Update;
						break;
					}

					yield return new(notificationKind, notification.DeviceInformation, notification.Driver, featureSet);
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
