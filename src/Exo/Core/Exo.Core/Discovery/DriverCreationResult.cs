using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class DriverCreationResult<TKey> : ComponentCreationResult<TKey, Driver>
	where TKey : notnull, IEquatable<TKey>
{
	public DriverCreationResult(ImmutableArray<TKey> registrationKeys, Driver driver)
		: this(registrationKeys, driver, null) { }

	public DriverCreationResult(ImmutableArray<TKey> registrationKeys, Driver driver, IAsyncDisposable? disposableResult)
		: base(registrationKeys, driver, disposableResult) { }
}
