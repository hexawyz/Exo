using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public abstract class ComponentCreationContext : IComponentCreationContext
{
	public abstract INestedDriverRegistryProvider DriverRegistry { get; }
	public abstract ILoggerFactory LoggerFactory { get; }

	public ValueTask<ImmutableArray<IAsyncDisposable>> CompleteAndResetAfterSuccessAsync(IAsyncDisposable? disposableResult)
		=> new(disposableResult is not null ? [disposableResult] : []);

	public ValueTask DisposeAndResetAsync() => ValueTask.CompletedTask;
}
