using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Exo.Service;

/// <summary>Device tracker for operating system devices.</summary>
public sealed class SystemDeviceDriverRegistry : ISystemDeviceDriverRegistry
{
	private readonly ConcurrentDictionary<string, ISystemDeviceDriver> _registeredDeviceDrivers = new(1, 20);
	private readonly HashSet<ISystemDeviceDriver> _registeredDrivers = new();
	private readonly object _lock = new();

	public bool TryGetDriver(string deviceName, [NotNullWhen(true)] out ISystemDeviceDriver? driver)
		=> _registeredDeviceDrivers.TryGetValue(deviceName, out driver);

	public bool TryRegisterDriver(ISystemDeviceDriver driver)
	{
		lock (_lock)
		{
			if (_registeredDrivers.Add(driver))
			{
				foreach (var deviceName in driver.GetDeviceNames())
				{
					if (!_registeredDeviceDrivers.TryAdd(deviceName, driver))
					{
						// Remove all previous entries.
						foreach (var deviceName2 in driver.GetDeviceNames())
						{
							_registeredDeviceDrivers.TryRemove(new KeyValuePair<string, ISystemDeviceDriver>(deviceName2, driver));
						}

						throw new InvalidOperationException("Another driver was already registered for the specified device");
					}
				}
				return true;
			}
			return false;
		}
	}

	public bool TryUnregisterDriver(ISystemDeviceDriver driver)
	{
		lock (_lock)
		{
			if (_registeredDrivers.Remove(driver))
			{
				foreach (var deviceName in driver.GetDeviceNames())
				{
					_registeredDeviceDrivers.TryRemove(new KeyValuePair<string, ISystemDeviceDriver>(deviceName, driver));
				}
				return true;
			}
			return false;
		}
	}
}
