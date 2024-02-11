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

	public ValueTask<ImmutableArray<IAsyncDisposable>> CompleteAndResetAfterSuccessAsync(IAsyncDisposable? disposableResult)
	{
		var nestedDriverRegistry = _nestedDriverRegistry;
		if (nestedDriverRegistry is not null)
		{
			_nestedDriverRegistry = null;
			if (nestedDriverRegistry.IsDisposed)
				nestedDriverRegistry = null;
		}
		return new
		(
			nestedDriverRegistry is not null ?
				disposableResult is not null ? [disposableResult, nestedDriverRegistry] : [nestedDriverRegistry] :
				disposableResult is not null ? [disposableResult] : []
		);
	}

	public virtual ValueTask DisposeAndResetAsync()
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
