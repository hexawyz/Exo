using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

/// <summary>Defines the context used to create a component.</summary>
/// <remarks>
/// Contexts will be used to provide data to component factories.
/// All properties exposed on a concrete implementation of <see cref="IComponentCreationContext"/> will be used to fill in parameters requested by a factory, if applicable.
/// </remarks>
public interface IComponentCreationContext : IAsyncDisposable
{
	/// <summary>The logger factory is a required abstraction for component creation.</summary>
	/// <remarks>
	/// It seems we can only go as far without injecting loggers into components and drivers.
	/// This will get special support for injecting <see cref="ILogger{TCategoryName}"/> into drivers when necessary.
	/// </remarks>
	ILoggerFactory LoggerFactory { get; }

	/// <summary>Collects disposables dependencies to be associated with the component.</summary>
	/// <remarks>
	/// <para>
	/// This method must return the list of objects that have to be disposed at the end of the component registration lifetime.
	/// References to these objects <b>MUST</b> be cleaned up from the instance, so that they are not Disposed by the call to <see cref="IAsyncDisposable.DisposeAsync"/>.
	/// </para>
	/// <para>
	/// The architecture here is designed so that it is possible to collect dependencies with minimal to no allocations.
	/// If called at all, the method <see cref="CollectDisposableDependencies"/> will always be called before <see cref="IAsyncDisposable.DisposeAsync"/>.
	/// After the call to <see cref="IAsyncDisposable.DisposeAsync"/>, an implementation can choose to reuse the instance.
	/// </para>
	/// </remarks>
	ImmutableArray<IAsyncDisposable> CollectDisposableDependencies();
}
