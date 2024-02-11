namespace Exo.Discovery;

public interface IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> : IDisposable
	where TKey : IEquatable<TKey>
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
{
	void HandleArrival(TDiscoveryContext context);
	void HandleRemoval(TKey key);
}
