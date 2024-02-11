namespace Exo.Discovery;

public static class DiscoveryOrchestratorExtensions
{
	public static IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> RegisterDiscoveryService<TDiscoveryService, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>(this IDiscoveryOrchestrator orchestrator, TDiscoveryService discoveryService)
		where TDiscoveryService : class, IDiscoveryService<TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TKey : notnull, IEquatable<TKey>
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>
		=> orchestrator.RegisterDiscoveryService<TDiscoveryService, SimpleComponentFactory<TCreationContext, TResult>, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>(discoveryService);
}

