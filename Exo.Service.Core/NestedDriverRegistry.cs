using System;
using System.Collections.Generic;

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
				_parentRegistry.RemoveDriver(driver);
			}
			_driverSet.Clear();
			_driverSet = null;
		}
	}

	public NestedDriverRegistry CreateNestedRegistry() => new NestedDriverRegistry(this);
	IDriverRegistry IDriverRegistry.CreateNestedRegistry() => CreateNestedRegistry();

	private void EnsureNotDisposed()
	{
		if (_driverSet is null) throw new ObjectDisposedException(nameof(NestedDriverRegistry));
	}

	public bool AddDriver(Driver driver)
	{
		lock (_parentRegistry.Lock)
		{
			EnsureNotDisposed();
			return AddDriverInLock(driver);
		}
	}

	private bool AddDriverInLock(Driver driver)
	{
		if (_driverSet is null) return false;
		if (_parentRegistry.AddDriver(driver))
		{
			_driverSet!.Add(driver);
			return true;
		}
		return false;
	}

	public bool RemoveDriver(Driver driver)
	{
		lock (_parentRegistry.Lock)
		{
			EnsureNotDisposed();
			return RemoveDriverInLock(driver);
		}
	}

	private bool RemoveDriverInLock(Driver driver)
	{
		if (_driverSet is null) return false;
		if (_parentRegistry.RemoveDriver(driver))
		{
			_driverSet!.Remove(driver);
			return true;
		}
		return false;
	}

	object IInternalDriverRegistry.Lock => _parentRegistry.Lock;

	bool IInternalDriverRegistry.AddDriver(Driver driver) => AddDriverInLock(driver);
	bool IInternalDriverRegistry.RemoveDriver(Driver driver) => RemoveDriverInLock(driver);
}
