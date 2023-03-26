using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

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

	private readonly object _lock = new();
	// Set of drivers that can only be accessed within the lock.
	private readonly HashSet<Driver> _driverSet = new();
	// List of drivers that can be readily accessed outside the lock.
	private ImmutableArray<Driver> _drivers = ImmutableArray<Driver>.Empty;
	// Cache of drivers per feature category.
	// We use ConditionalWeakTable instead of ConcurrentDictionary in order to allow assembly unloading.
	// Also, this should never be updated outside the lock in order to keep coherency with _driverSet.
	private readonly ConditionalWeakTable<Type, FeatureCacheEntry> _featureCache = new();

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
		if (_driverSet.Add(driver))
		{
			_drivers = _driverSet.ToImmutableArray();

			foreach (var kvp in _featureCache)
			{
				kvp.Value.TryAdd(driver);
			}

			return true;
		}
		return false;
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
		if (_driverSet.Remove(driver))
		{
			_drivers = _driverSet.ToImmutableArray();

			foreach (var kvp in _featureCache)
			{
				kvp.Value.TryRemove(driver);
			}

			return true;
		}
		return false;
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
}
