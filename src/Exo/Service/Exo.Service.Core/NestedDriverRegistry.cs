namespace Exo.Service;

public sealed class NestedDriverRegistry : IDriverRegistry, IInternalDriverRegistry
{
	private readonly IInternalDriverRegistry _parentRegistry;
	private HashSet<Driver>? _driverSet;

	internal NestedDriverRegistry(IInternalDriverRegistry parentRegistry)
	{
		_parentRegistry = parentRegistry;
		_driverSet = new();
	}

	public void Dispose()
	{
		lock (this)
		{
			if (_driverSet is null) return;

			foreach (var driver in _driverSet)
			{
				_parentRegistry.RemoveDriverAsync(driver);
			}
			_driverSet.Clear();
			_driverSet = null;
		}
	}

	public NestedDriverRegistry CreateNestedRegistry() => new NestedDriverRegistry(this);
	IDriverRegistry INestedDriverRegistryProvider.CreateNestedRegistry() => CreateNestedRegistry();

	private void EnsureNotDisposed()
	{
		if (_driverSet is null) throw new ObjectDisposedException(nameof(NestedDriverRegistry));
	}

	public async ValueTask<bool> AddDriverAsync(Driver driver)
	{
		using (await _parentRegistry.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			EnsureNotDisposed();
			return await AddDriverInLock(driver).ConfigureAwait(false);
		}
	}

	private async ValueTask<bool> AddDriverInLock(Driver driver)
	{
		if (_driverSet is null) return false;
		if (await _parentRegistry.AddDriverAsync(driver).ConfigureAwait(false))
		{
			_driverSet!.Add(driver);
			return true;
		}
		return false;
	}

	public async ValueTask<bool> RemoveDriverAsync(Driver driver)
	{
		using (await _parentRegistry.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			EnsureNotDisposed();
			return await RemoveDriverInLock(driver).ConfigureAwait(false);
		}
	}

	private async ValueTask<bool> RemoveDriverInLock(Driver driver)
	{
		if (_driverSet is null) return false;
		if (await _parentRegistry.RemoveDriverAsync(driver).ConfigureAwait(false))
		{
			_driverSet!.Remove(driver);
			return true;
		}
		return false;
	}

	AsyncLock IInternalDriverRegistry.Lock => _parentRegistry.Lock;

	ValueTask<bool> IInternalDriverRegistry.AddDriverAsync(Driver driver) => AddDriverInLock(driver);
	ValueTask<bool> IInternalDriverRegistry.RemoveDriverAsync(Driver driver) => RemoveDriverInLock(driver);
}
