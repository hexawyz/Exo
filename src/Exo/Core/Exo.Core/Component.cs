namespace Exo;

public abstract class Component : IAsyncDisposable
{
	public abstract string FriendlyName { get; }

	public abstract ValueTask DisposeAsync();
}
