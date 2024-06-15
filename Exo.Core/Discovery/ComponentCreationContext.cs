using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public abstract class ComponentCreationContext : IComponentCreationContext
{
	public abstract INestedDriverRegistryProvider DriverRegistry { get; }
	public abstract ILoggerFactory LoggerFactory { get; }

	public ImmutableArray<IAsyncDisposable> CollectDisposableDependencies() => [];
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
