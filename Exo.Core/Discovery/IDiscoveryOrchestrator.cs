namespace Exo.Discovery;

public interface IDiscoveryOrchestrator
{
	IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> RegisterDiscoveryService<TDiscoveryService, TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>(TDiscoveryService discoveryService)
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TFactory : class, Delegate
		where TKey : notnull, IEquatable<TKey>
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>;
}
