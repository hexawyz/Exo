using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace Exo.Service;

public sealed class DriverRegistry : IDriverRegistry, IInternalDriverRegistry, IDeviceWatcher
{
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

	// TODO: This must be moved into configuration.
	// This assigns a unique ID to each device based on the configuration key. It is needed so that device settings can be restored.
	private static readonly ConcurrentDictionary<string, Guid> DeviceUniqueIdDictionary = new();

	private static Guid GetDeviceUniqueId(DeviceConfigurationKey configurationKey)
		=> DeviceUniqueIdDictionary.GetOrAdd(configurationKey.DeviceMainId, _ => Guid.NewGuid());

	private readonly object _lock = new();

	// Set of drivers that can only be accessed within the lock.
	private readonly Dictionary<Driver, DeviceInformation> _deviceInformationsByDriver = new();

	// Map of drivers by device ID that can only be updated within the lock.
	private readonly ConcurrentDictionary<Guid, Driver> _driversByUniqueId = new();

	private Action<bool, Driver, DeviceInformation>[]? _driverUpdated;

	object IInternalDriverRegistry.Lock => _lock;

	bool IInternalDriverRegistry.AddDriver(Driver driver) => AddDriverInLock(driver);
	bool IInternalDriverRegistry.RemoveDriver(Driver driver) => RemoveDriverInLock(driver);

	public void Dispose() { }

	public NestedDriverRegistry CreateNestedRegistry() => new(this);
	IDriverRegistry IDriverRegistry.CreateNestedRegistry() => CreateNestedRegistry();

	public bool AddDriver(Driver driver)
	{
		lock (_lock)
		{
			return AddDriverInLock(driver);
		}
	}

	private bool AddDriverInLock(Driver driver)
	{
		// TODO: Make the GUID persistent in the configuration. (So that a given device gets the same GUID everytime)
		// Of course, the GUID shall be associated with the device configuration key.
		var deviceId = GetDeviceUniqueId(driver.ConfigurationKey);
		var driverType = driver.GetType();
		var deviceInformation = new DeviceInformation(deviceId, driver.FriendlyName, driver.DeviceCategory, GetDriverFeatures(driverType), driverType);

		if (_deviceInformationsByDriver.TryAdd(driver, deviceInformation))
		{
			_driversByUniqueId[deviceId] = driver;

			try
			{
				_driverUpdated.Invoke(true, driver, deviceInformation);
			}
			catch (AggregateException)
			{
				// TODO: Log
			}
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool RemoveDriver(Driver driver)
	{
		lock (_lock)
		{
			return RemoveDriverInLock(driver);
		}
	}

	internal bool RemoveDriverInLock(Driver driver)
	{
		if (_deviceInformationsByDriver.Remove(driver, out var deviceInformation))
		{
			_driversByUniqueId.TryRemove(new(deviceInformation.Id, driver));

			try
			{
				_driverUpdated.Invoke(false, driver, deviceInformation);
			}
			catch (AggregateException)
			{
				// TODO: Log
			}
			return true;
		}
		else
		{
			return false;
		}
	}

	private static readonly UnboundedChannelOptions WatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };

	/// <summary>Watch for devices.</summary>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ChannelReader<(bool IsAdded, DeviceInformation deviceInformation, Driver Driver)> reader;

		var channel = Channel.CreateUnbounded<(bool IsAdded, DeviceInformation deviceInformation, Driver Driver)>(WatchChannelOptions);
		reader = channel.Reader;
		var writer = channel.Writer;

		var onDriverUpdated = (bool b, Driver d, DeviceInformation di) => { writer.TryWrite((b, di, d)); };

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceInformationsByDriver.Count, 10));
			foreach (var kvp in _deviceInformationsByDriver)
			{
				initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, kvp.Value, kvp.Key);
			}

			ArrayExtensions.InterlockedAdd(ref _driverUpdated, onDriverUpdated);
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

			await foreach (var (isAdded, deviceInformation, driver) in reader.ReadAllAsync(cancellationToken))
			{
				yield return new(isAdded ? WatchNotificationKind.Addition : WatchNotificationKind.Removal, deviceInformation, driver);
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _driverUpdated, onDriverUpdated);
		}
	}

	/// <summary>Watch for devices providing the specified feature set.</summary>
	/// <remarks>
	/// Just because a driver implements <see cref="IDeviceDriver{TFeature}"/> does not necessarily means that the instance actually exposes any such feature.
	/// Drivers should generally not expose feature sets that they do not support, but some specific devices may end up having no corresponding features.
	/// Such device drivers will still be reported by this method. It is up to the consumer of the notifications to decide what to do with the devices.
	/// </remarks>
	/// <param name="cancellationToken">A token used to cancel the watch operation.</param>
	/// <returns>An asynchronous enumerable providing live access to all devices.</returns>
	public async IAsyncEnumerable<DeviceWatchNotification> WatchAsync<TFeature>([EnumeratorCancellation] CancellationToken cancellationToken)
		where TFeature : class, IDeviceFeature
	{
		ChannelReader<(bool IsAdded, DeviceInformation deviceInformation, Driver? Driver)> reader;

		var channel = Channel.CreateUnbounded<(bool IsAdded, DeviceInformation deviceInformation, Driver? Driver)>(WatchChannelOptions);
		reader = channel.Reader;
		var writer = channel.Writer;

		var onDriverUpdated = (bool b, Driver d, DeviceInformation di) => { if (d is IDeviceDriver<TFeature>) writer.TryWrite((b, di, b ? d : null)); };

		DeviceWatchNotification[]? initialNotifications;
		int initialNotificationCount = 0;

		lock (_lock)
		{
			initialNotifications = ArrayPool<DeviceWatchNotification>.Shared.Rent(Math.Min(_deviceInformationsByDriver.Count, 10));
			foreach (var kvp in _deviceInformationsByDriver)
			{
				if (kvp.Key is IDeviceDriver<TFeature>)
				{
					initialNotifications[initialNotificationCount++] = new(WatchNotificationKind.Enumeration, kvp.Value, kvp.Key);
				}
			}

			ArrayExtensions.InterlockedAdd(ref _driverUpdated, onDriverUpdated);
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

			await foreach (var (isAdded, deviceInformation, driver) in reader.ReadAllAsync(cancellationToken))
			{
				yield return new(isAdded ? WatchNotificationKind.Addition : WatchNotificationKind.Removal, deviceInformation, driver);
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _driverUpdated, onDriverUpdated);
		}
	}

	public bool TryGetDriver(Guid deviceId, [NotNullWhen(true)] out Driver? driver)
		=> _driversByUniqueId.TryGetValue(deviceId, out driver);

	public bool TryGetDeviceId(Driver driver, [NotNullWhen(true)] out Guid deviceId)
	{
		if (_deviceInformationsByDriver.TryGetValue(driver, out var info))
		{
			deviceId = info.Id;
			return true;
		}
		else
		{
			deviceId = default;
			return false;
		}
	}		
}
