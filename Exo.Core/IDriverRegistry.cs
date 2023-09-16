namespace Exo;

public interface IDriverRegistry : IDisposable
{
	IDriverRegistry CreateNestedRegistry();
	ValueTask<bool> AddDriverAsync(Driver driver);
	ValueTask<bool> RemoveDriverAsync(Driver driver);
}
