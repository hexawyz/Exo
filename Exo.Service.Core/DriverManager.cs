using System.Collections.Generic;
using System.Collections.Immutable;
using Exo.Core;

namespace Exo.Service;

public class DriverManager
{
	private readonly object _lock = new();
	// Set of drivers that can only be accessed within the lock.
	private readonly HashSet<Driver> _driverSet = new();
	// List of drivers that can be readily accessed outside the lock.
	private ImmutableArray<Driver> _drivers = ImmutableArray<Driver>.Empty;

	public bool AddDriver(Driver driver)
	{
		lock (_lock)
		{
			if (_driverSet.Add(driver))
			{
				_drivers = _driverSet.ToImmutableArray();
				return true;
			}
		}
		return false;
	}

	public bool RemoveDriver(Driver driver)
	{
		lock (_lock)
		{
			if (_driverSet.Remove(driver))
			{
				_drivers = _driverSet.ToImmutableArray();
				return true;
			}
		}
		return false;
	}

	public ImmutableArray<Driver> GetDrivers() => _drivers;

	public ImmutableArray<IDeviceDriver<TFeature>> GetDrivers<TFeature>()
		where TFeature : class, IDeviceFeature
	{
		var drivers = ImmutableArray.CreateBuilder<IDeviceDriver<TFeature>>();

		foreach (var driver in _drivers)
		{
			if (driver is IDeviceDriver<TFeature> specificDriver)
			{
				drivers.Add(specificDriver);
			}
		}

		return drivers.ToImmutable();
	}
}
