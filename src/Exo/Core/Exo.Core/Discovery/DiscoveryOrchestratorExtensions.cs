namespace Exo.Discovery;

public static class DiscoveryOrchestratorExtensions
{
	public static ValueTask<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> RegisterDiscoveryServiceAsync<TDiscoveryService, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>(this IDiscoveryOrchestrator orchestrator, TDiscoveryService discoveryService)
		where TDiscoveryService : class, IDiscoveryService<TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TKey : notnull, IEquatable<TKey>
		where TParsedFactoryDetails : notnull
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>
		=> orchestrator.RegisterDiscoveryServiceAsync<TDiscoveryService, SimpleComponentFactory<TCreationContext, TResult>, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>(discoveryService);
}

