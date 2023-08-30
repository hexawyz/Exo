using Microsoft.Extensions.Logging;

namespace Exo;

public interface IDriverCreationContext<TResult>
	where TResult : IDriverCreationResult
{
	/// <summary>The logger factory is a required abstraction for driver creation.</summary>
	/// <remarks>
	/// It seems we can only go as far without injecting loggers into drivers.
	/// This will get special support for injecting <see cref="ILogger{TCategoryName}"/> into drivers when necessary.
	/// </remarks>
	ILoggerFactory LoggerFactory { get; }

	TResult CompleteAndReset(Driver driver);
	ValueTask DisposeAndResetAsync();
}
