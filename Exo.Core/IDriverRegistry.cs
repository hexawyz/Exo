using System;

namespace Exo;

public interface IDriverRegistry : IDisposable
{
	IDriverRegistry CreateNestedRegistry();
	bool AddDriver(Driver driver);
	bool RemoveDriver(Driver driver);
}
