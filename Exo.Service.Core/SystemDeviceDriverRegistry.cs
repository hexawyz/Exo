using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Exo.Core;

namespace Exo.Service
{
	/// <summary>Device tracker for operating system devices.</summary>
	public sealed class SystemDeviceDriverRegistry : ISystemDeviceDriverRegistry
	{
		private readonly ConcurrentDictionary<string, Driver> _registeredDrivers = new();

		public void RegisterDriver(Driver driver, string deviceName)
		{
			if (!_registeredDrivers.TryAdd(deviceName, driver))
			{
				throw new InvalidOperationException("Another driver was already registered for the specified device");
			}
		}

		public bool TryGetDriver(string deviceName, [NotNullWhen(true)] out Driver? driver)
			=> _registeredDrivers.TryGetValue(deviceName, out driver);
	}
}
