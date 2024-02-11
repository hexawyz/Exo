namespace Exo;

public interface IDriverRegistry : IDisposable, INestedDriverRegistryProvider
{
	ValueTask<bool> AddDriverAsync(Driver driver);
	ValueTask<bool> RemoveDriverAsync(Driver driver);
}
