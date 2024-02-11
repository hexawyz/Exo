using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

/// <summary>Defines the context used to create a component.</summary>
/// <remarks>
/// Contexts will be used to provide data to component factories.
/// All properties exposed on a concrete implementation of <see cref="IComponentCreationContext"/> will be used to fill in parameters requested by a factory, if applicable.
/// </remarks>
public interface IComponentCreationContext
{
	/// <summary>The logger factory is a required abstraction for component creation.</summary>
	/// <remarks>
	/// It seems we can only go as far without injecting loggers into components and drivers.
	/// This will get special support for injecting <see cref="ILogger{TCategoryName}"/> into drivers when necessary.
	/// </remarks>
	ILoggerFactory LoggerFactory { get; }

	/// <summary>Resets the context after a successful component creation.</summary>
	/// <remarks>
	/// This method must return the list of objects that have to be disposed at the end of the component registration lifetime.
	/// This should include <paramref name="disposableResult"/> as necessary.
	/// </remarks>
	ValueTask<ImmutableArray<IAsyncDisposable>> CompleteAndResetAfterSuccessAsync(IAsyncDisposable? disposableResult);

	/// <summary>Disposes allocated resources and resets the context.</summary>
	/// <remarks>Contexts must be usable for another creation operation once this method is called.</remarks>
	/// <returns></returns>
	ValueTask DisposeAndResetAsync();
}
