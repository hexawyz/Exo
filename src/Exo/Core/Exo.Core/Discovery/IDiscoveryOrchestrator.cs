namespace Exo.Discovery;

public interface IDiscoveryOrchestrator
{
	ValueTask<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> RegisterDiscoveryServiceAsync<TDiscoveryService, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>(TDiscoveryService discoveryService)
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>, IJsonTypeInfoProvider<TParsedFactoryDetails>
		where TFactory : class, Delegate
		where TKey : notnull, IEquatable<TKey>
		where TParsedFactoryDetails : notnull
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>;
}
