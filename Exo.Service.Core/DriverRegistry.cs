using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Exo.Service;

public sealed class DriverRegistry : IDriverRegistry, IInternalDriverRegistry
{
	private abstract class FeatureCacheEntry
	{
		// This method must be called from within the registry lock.
		public abstract void TryAdd(Driver driver);
		// This method must be called from within the registry lock.
		public abstract void TryRemove(Driver driver);
	}

	private sealed class FeatureCacheEntry<TFeature> : FeatureCacheEntry
		where TFeature : class, IDeviceFeature
	{
		private IDeviceDriver<TFeature>[] _features;

		public FeatureCacheEntry(IDeviceDriver<TFeature>[] features) => _features = features;

		public ImmutableArray<IDeviceDriver<TFeature>> Features
		{
			get
			{
				var features = Volatile.Read(ref _features);
				return Unsafe.As<IDeviceDriver<TFeature>[], ImmutableArray<IDeviceDriver<TFeature>>>(ref features);
			}
		}

		public override void TryAdd(Driver driver)
		{
			if (driver is IDeviceDriver<TFeature> feature)
			{
				var features = _features;
				Array.Resize(ref features, features.Length + 1);
				features[^1] = feature;
			}
		}

		public override void TryRemove(Driver driver)
		{
			if (driver is IDeviceDriver<TFeature> feature)
			{
				var features = _features;

				if (features.Length == 1)
				{
					Volatile.Write(ref _features, Array.Empty<IDeviceDriver<TFeature>>());
				}
				else
				{
					int index = Array.IndexOf(features, feature);

					if (index >= 0)
					{
						var newFeatures = new IDeviceDriver<TFeature>[features.Length - 1];

						Array.Copy(features, 0, newFeatures, 0, index);
						Array.Copy(features, index + 1, newFeatures, index, newFeatures.Length - index);

						Volatile.Write(ref _features, newFeatures);
					}
				}
			}
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

	private readonly object _lock = new();
	// Set of drivers that can only be accessed within the lock.
	private readonly Dictionary<Driver, DeviceInformation> _driverDictionary = new();
	// List of drivers that can be readily accessed outside the lock.
	private ImmutableArray<Driver> _drivers = ImmutableArray<Driver>.Empty;
	// Cache of drivers per feature category.
	// We use ConditionalWeakTable instead of ConcurrentDictionary in order to allow assembly unloading.
	// Also, this should never be updated outside the lock in order to keep coherency with _driverSet.
	private readonly ConditionalWeakTable<Type, FeatureCacheEntry> _featureCache = new();

	private Action<Driver, DeviceInformation>? _driverAdded;
	private Action<DeviceInformation>? _driverRemoved;

	object IInternalDriverRegistry.Lock => _lock;

	bool IInternalDriverRegistry.AddDriver(Driver driver) => AddDriverInLock(driver);
	bool IInternalDriverRegistry.RemoveDriver(Driver driver) => RemoveDriverInLock(driver);

	public void Dispose() { }

	public NestedDriverRegistry CreateNestedRegistry() => new NestedDriverRegistry(this);
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
		// TODO: Derivate a new Unique ID from the configuration key. (Maybe a GUID)
		var driverType = driver.GetType();
		var deviceInformation = new DeviceInformation(driver.ConfigurationKey.DeviceMainId, driver.FriendlyName, driver.DeviceCategory, GetDriverFeatures(driverType), driverType);

		if (_driverDictionary.TryAdd(driver, deviceInformation))
		{
			_drivers = _driverDictionary.Keys.ToImmutableArray();

			foreach (var kvp in _featureCache)
			{
				kvp.Value.TryAdd(driver);
			}

			_driverAdded?.Invoke(driver, deviceInformation);
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
		if (_driverDictionary.Remove(driver, out var deviceInformation))
		{
			_drivers = _driverDictionary.Keys.ToImmutableArray();

			foreach (var kvp in _featureCache)
			{
				kvp.Value.TryRemove(driver);
			}

			_driverRemoved?.Invoke(deviceInformation);
			return true;
		}
		else
		{
			return false;
		}
	}

	public ImmutableArray<Driver> GetDrivers() => _drivers;

	public ImmutableArray<IDeviceDriver<TFeature>> GetDrivers<TFeature>()
		where TFeature : class, IDeviceFeature
	{
		if (!_featureCache.TryGetValue(typeof(TFeature), out var entry))
		{
			lock (_lock)
			{
				if (!_featureCache.TryGetValue(typeof(TFeature), out entry))
				{
					entry = new FeatureCacheEntry<TFeature>(GetDriversSlow<TFeature>(_drivers));
					_featureCache.TryAdd(typeof(TFeature), entry);
				}
			}
		}

		return Unsafe.As<FeatureCacheEntry, FeatureCacheEntry<TFeature>>(ref entry).Features;
	}

	private static IDeviceDriver<TFeature>[] GetDriversSlow<TFeature>(ImmutableArray<Driver> drivers)
		where TFeature : class, IDeviceFeature
	{
		var featureDrivers = new List<IDeviceDriver<TFeature>>();

		foreach (var driver in featureDrivers)
		{
			if (driver is IDeviceDriver<TFeature> specificDriver)
			{
				featureDrivers.Add(specificDriver);
			}
		}

		return featureDrivers.ToArray();
	}

	private static readonly UnboundedChannelOptions WatchChannelOptions = new() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false };

	public async IAsyncEnumerable<DriverWatchNotification> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ChannelReader<(bool IsAdded, DeviceInformation deviceInformation, Driver? Driver)> reader;
		Action<Driver, DeviceInformation> onDriverAdded;
		Action<DeviceInformation> onDriverRemoved;

		lock (_lock)
		{
			foreach (var kvp in _driverDictionary)
			{
				yield return new(DriverWatchNotificationKind.Enumeration, kvp.Value, kvp.Key);
			}

			cancellationToken.ThrowIfCancellationRequested();

			var channel = Channel.CreateUnbounded<(bool IsAdded, DeviceInformation deviceInformation, Driver? Driver)>(WatchChannelOptions);
			reader = channel.Reader;
			var writer = channel.Writer;

			onDriverAdded = (d, di) => writer.TryWrite((true, di, d));
			onDriverRemoved = di => writer.TryWrite((false, di, null));

			_driverAdded += onDriverAdded;
			_driverRemoved += onDriverRemoved;
		}
		try
		{
			await foreach (var (isAdded, deviceInformation, driver) in reader.ReadAllAsync(cancellationToken))
			{
				yield return new(isAdded ? DriverWatchNotificationKind.Addition : DriverWatchNotificationKind.Removal, deviceInformation, driver);
			}
		}
		finally
		{
			lock (_lock)
			{
				_driverRemoved -= onDriverRemoved;
				_driverAdded -= onDriverAdded;
			}
		}
	}
}

public enum DriverWatchNotificationKind
{
	Enumeration = 0,
	Addition = 1,
	Removal = 2,
}

public readonly struct DriverWatchNotification
{
	public DriverWatchNotification(DriverWatchNotificationKind kind, DeviceInformation driverInformation, Driver? driver)
	{
		Kind = kind;
		DeviceInformation = driverInformation;
		Driver = driver;
	}

	public DriverWatchNotificationKind Kind { get; }
	public DeviceInformation DeviceInformation { get; }
	public Driver? Driver { get; }
}

public sealed class DeviceInformation
{
	public DeviceInformation(string uniqueId, string friendlyName, DeviceCategory category, Type[] featureTypes, Type driverType)
	{
		UniqueId = uniqueId;
		FriendlyName = friendlyName;
		Category = category;
		FeatureTypes = featureTypes;
		DriverType = driverType;
	}

	public string UniqueId { get; }
	public string FriendlyName { get; }
	public DeviceCategory Category { get; }
	public Type[] FeatureTypes { get; }
	public Type DriverType { get; }
}
