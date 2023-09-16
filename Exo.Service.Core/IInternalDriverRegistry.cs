namespace Exo.Service;

internal interface IInternalDriverRegistry
{
	AsyncLock Lock { get; }
	ValueTask<bool> AddDriverAsync(Driver driver);
	ValueTask<bool> RemoveDriverAsync(Driver driver);
}
