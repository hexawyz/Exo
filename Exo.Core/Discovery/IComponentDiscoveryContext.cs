using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

/// <summary>Represents the context of a component discovery.</summary>
/// <remarks>
/// This context exposes details regarding the discovery of a component, and allows creating the creation context.
/// </remarks>
/// <typeparam name="TKey">The type of discovery key associated with this context.</typeparam>
/// <typeparam name="TCreationContext">The type of component creation context to be used for component creation.</typeparam>
public interface IComponentDiscoveryContext<TKey, TCreationContext>
	where TKey : IEquatable<TKey>
	where TCreationContext : class, IComponentCreationContext
{
	/// <summary>Gets the keys that were discovered.</summary>
	/// <remarks>
	/// <para>
	/// At least one key must be provided, but the discovery source is not required to provide all the keys at that moment.
	/// Especially in the case of system devices, the single discovered device path can be provided here, while the other keys are resolved during the call to <see cref="PrepareForCreationAsync(INestedDriverRegistryProvider, ILoggerFactory)"/>.
	/// </para>
	/// <para>
	/// Providing a quickly computed set of keys here is an efficient way for the discovery orchestrator to determine if the corresponding component is already initialized.
	/// If the component is not yet initialized, the costlier context creation by <see cref="PrepareForCreationAsync(INestedDriverRegistryProvider, ILoggerFactory)"/> will be invocated.
	/// </para>
	/// </remarks>
	ImmutableArray<TKey> DiscoveredKeys { get; }

	ValueTask<ComponentCreationParameters<TKey, TCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken);
}
