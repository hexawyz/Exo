using System.Collections.Immutable;
using System.Reflection;

namespace Exo.Discovery;

public interface IDiscoveryService<TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TFactory : Delegate
	where TKey : notnull, IEquatable<TKey>
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	string FriendlyName { get; }

	bool RegisterFactory(Guid factoryId, ImmutableArray<CustomAttributeData> attributes);

	ValueTask StartAsync(CancellationToken cancellationToken);

	ValueTask<TResult?> InvokeFactoryAsync
	(
		TFactory factory,
		ComponentCreationParameters<TKey, TCreationContext> creationParameters,
		CancellationToken cancellationToken
	);
}

public interface IDiscoveryService<TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
	: IDiscoveryService<SimpleComponentFactory<TCreationContext, TResult>, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TKey : notnull, IEquatable<TKey>
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	ValueTask<TResult?> IDiscoveryService<SimpleComponentFactory<TCreationContext, TResult>, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>.InvokeFactoryAsync
	(
		SimpleComponentFactory<TCreationContext, TResult> factory,
		ComponentCreationParameters<TKey, TCreationContext> creationParameters,
		CancellationToken cancellationToken
	) => factory(creationParameters.CreationContext, cancellationToken);
}
