using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Exo.I2C;

namespace Exo.Service;

internal class I2CBusRegistry : II2CBusRegistry, II2CBusProvider
{
	private class Registration : IDisposable
	{
		private ConcurrentDictionary<string, object>? _registeredBusses;
		private readonly string _deviceName;
		private readonly MonitorI2CBusResolver _resolver;

		public Registration(ConcurrentDictionary<string, object> registeredBusses, string deviceName, MonitorI2CBusResolver resolver)
		{
			_registeredBusses = registeredBusses;
			_deviceName = deviceName;
			_resolver = resolver;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _registeredBusses, null) is { } registeredBuses)
			{
				registeredBuses.TryRemove(new(_deviceName, _resolver));
			}
		}
	}

	private readonly ConcurrentDictionary<string, object> _registeredBusses = new();

	public IDisposable RegisterBusResolver(string deviceName, MonitorI2CBusResolver resolver)
	{
		var registration = new Registration(_registeredBusses, deviceName, resolver);

		while (!_registeredBusses.TryAdd(deviceName, resolver))
		{
			if (!_registeredBusses.TryGetValue(deviceName, out var other)) continue;

			if (other is TaskCompletionSource<MonitorI2CBusResolver> tcs)
			{
				if (!_registeredBusses.TryUpdate(deviceName, resolver, other)) continue;

				tcs.TrySetResult(resolver);
				break;
			}
		}

		return registration;
	}

	public ValueTask<MonitorI2CBusResolver> GetMonitorBusResolverAsync(string deviceName, CancellationToken cancellationToken)
	{
		TaskCompletionSource<MonitorI2CBusResolver>? tcs = null;
		while (true)
		{
			if (_registeredBusses.TryGetValue(deviceName, out var obj))
			{
				if (obj is MonitorI2CBusResolver resolver) return new(resolver);

				return new(Unsafe.As<TaskCompletionSource<MonitorI2CBusResolver>>(obj).Task.WaitAsync(cancellationToken));
			}
			else
			{
				tcs ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
				if (_registeredBusses.TryAdd(deviceName, tcs))
				{
					return new(tcs.Task.WaitAsync(cancellationToken));
				}
			}
		}
	}
}
