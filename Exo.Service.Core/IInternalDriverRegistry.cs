namespace Exo.Service;

internal interface IInternalDriverRegistry
{
	object Lock { get; }
	bool AddDriver(Driver driver);
	bool RemoveDriver(Driver driver);
}
