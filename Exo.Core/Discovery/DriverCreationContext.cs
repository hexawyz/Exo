using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public abstract class DriverCreationContext : IComponentCreationContext
{
	protected abstract INestedDriverRegistryProvider NestedDriverRegistryProvider { get; }
	public abstract ILoggerFactory LoggerFactory { get; }
	private Optional<IDriverRegistry>? _nestedDriverRegistry;

	public Optional<IDriverRegistry> DriverRegistry => _nestedDriverRegistry ??= new OptionalNestedDriverRegistry(NestedDriverRegistryProvider);

	public DriverCreationContext()
	{
	}

	public ImmutableArray<IAsyncDisposable> CollectDisposableDependencies()
	{
		var builder = new DisposableDependencyBuilder();
		CollectDisposableDependencies(ref builder);
		return builder.ToImmutableArray();
	}

	protected virtual void CollectDisposableDependencies(ref DisposableDependencyBuilder builder)
	{
		var nestedDriverRegistry = _nestedDriverRegistry;
		if (nestedDriverRegistry is not null)
		{
			_nestedDriverRegistry = null;
			if (!nestedDriverRegistry.IsDisposed)
			{
				builder.Add(nestedDriverRegistry);
			}
		}
	}

	public virtual ValueTask DisposeAsync()
	{
		var nestedDriverRegistry = _nestedDriverRegistry;
		if (nestedDriverRegistry is not null)
		{
			_nestedDriverRegistry = null;
			nestedDriverRegistry.Dispose();
		}
		return ValueTask.CompletedTask;
	}
}
