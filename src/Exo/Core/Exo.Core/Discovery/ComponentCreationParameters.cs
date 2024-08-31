using System.Collections.Immutable;

namespace Exo.Discovery;

public readonly struct ComponentCreationParameters<TKey, TCreationContext> : IAsyncDisposable
	where TKey : IEquatable<TKey>
	where TCreationContext : class, IComponentCreationContext
{
	public ImmutableArray<TKey> AssociatedKeys { get; }
	public TCreationContext CreationContext { get; }
	public ImmutableArray<Guid> FactoryIds { get; }

	public ComponentCreationParameters(ImmutableArray<TKey> associatedKeys, TCreationContext creationContext, ImmutableArray<Guid> factoryIds)
	{
		AssociatedKeys = associatedKeys;
		CreationContext = creationContext;
		FactoryIds = factoryIds;
	}

	public ValueTask DisposeAsync() => CreationContext.DisposeAsync();
}
