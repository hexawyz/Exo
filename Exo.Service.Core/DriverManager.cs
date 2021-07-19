using System;
using System.Collections.Generic;
using System.Linq;
using Exo.Core;

namespace Exo.Service
{
	public class DriverManager
	{
		private readonly object _lock = new();
		// Set of drivers that can only be accessed within the lock.
		private readonly HashSet<Driver> _driverSet = new();
		// List of drivers that can be readily accessed outside the lock.
		private Driver[] _drivers = Array.Empty<Driver>();

		public bool AddDriver(Driver driver)
		{
			lock (_lock)
			{
				if (_driverSet.Add(driver))
				{
					_drivers = _driverSet.ToArray();
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
					_drivers = _driverSet.ToArray();
					return true;
				}
			}
			return false;
		}

		public IDeviceDriver<TFeature>[] GetDrivers<TFeature>()
			where TFeature : IDeviceFeature
		{
			throw new NotImplementedException();
		}
	}
}
