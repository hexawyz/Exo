using System.Runtime.CompilerServices;
using Exo.SystemManagementBus;

namespace Exo.Service;

internal class SystemManagementBusRegistry : ISystemManagementBusRegistry, ISystemManagementBusProvider, IDisposable
{
	private class Registration : IDisposable
	{
		private SystemManagementBusRegistry? _registry;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _registry, null) is { } registry && Volatile.Read(ref registry._lock) is { } @lock)
			{
				lock (@lock)
				{
					registry._registeredBus = new TaskCompletionSource<ISystemManagementBus>(TaskCreationOptions.RunContinuationsAsynchronously);
				}
			}
		}
	}

	private object? _lock = new();
	private object? _registeredBus = new TaskCompletionSource<ISystemManagementBus>(TaskCreationOptions.RunContinuationsAsynchronously);

	private object Lock => Volatile.Read(ref _lock) ?? throw new ObjectDisposedException(nameof(SystemManagementBusRegistry));

	public void Dispose()
	{
		if (Volatile.Read(ref _lock) is { } @lock)
		{
			lock (@lock)
			{
				Volatile.Write(ref _lock, null);
			}
		}
	}

	public IDisposable RegisterSystemBus(ISystemManagementBus bus)
	{
		lock (Lock)
		{
			var registeredBus = _registeredBus ?? throw new ObjectDisposedException(nameof(SystemManagementBusRegistry));

			if (registeredBus is TaskCompletionSource<ISystemManagementBus> tcs)
			{
				tcs.TrySetResult(bus);
				Volatile.Write(ref _registeredBus, bus);
				return new Registration();
			}

			// NB: Although we could have multiple alternative ways of providing SMBus access, the choice is probably better addressed at the motherboard driver level.
			throw new InvalidOperationException("A SMBus implementation is already registered.");
		}
	}

	public ValueTask<ISystemManagementBus> GetSystemBusAsync(CancellationToken cancellationToken)
	{
		lock (Lock)
		{
			var registeredBus = _registeredBus ?? throw new ObjectDisposedException(nameof(SystemManagementBusRegistry));

			if (registeredBus is TaskCompletionSource<ISystemManagementBus> tcs)
			{
				return new(tcs.Task.WaitAsync(cancellationToken));
			}

			return new(Unsafe.As<ISystemManagementBus>(registeredBus));
		}
	}
}
