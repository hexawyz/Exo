using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Exo.Discovery;

/// <summary>Represents a discovery service.</summary>
/// <remarks>
/// <para>
/// <typeparamref name="TParsedFactoryDetails"/> must have a <see cref="TypeIdAttribute"/> applied in order for it to be serializable in the configuration system.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The type of factory used to instantiate components.</typeparam>
/// <typeparam name="TKey">The type of key used to register components.</typeparam>
/// <typeparam name="TParsedFactoryDetails">The type of details to </typeparam>
/// <typeparam name="TDiscoveryContext">The type of context used when running the discovery process for a specific key or set of keys.</typeparam>
/// <typeparam name="TCreationContext">The type of context used to call the factories that will instantiate components.</typeparam>
/// <typeparam name="TComponent">The type of components handled by the factory.</typeparam>
/// <typeparam name="TResult">The type of result that will be produced by factories.</typeparam>
public interface IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TFactory : Delegate
	where TKey : notnull, IEquatable<TKey>
	where TParsedFactoryDetails : notnull
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	string FriendlyName { get; }

	/// <summary>Parses factory attributes into service-specific details.</summary>
	/// <remarks>The details must contain all the information necessary to register the factory with <see cref="RegisterFactory(Guid, TParsedFactoryDetails)"/>.</remarks>
	/// <param name="attributes">Attributes associated with the factory method.</param>
	/// <param name="parsedFactoryDetails">Details that were parsed for the factory.</param>
	/// <returns></returns>
	bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out TParsedFactoryDetails? parsedFactoryDetails);

	/// <summary>Registers a known factory using previously parsed details.</summary>
	/// <remarks>
	/// <para>
	/// This method should produces the same results as <see cref="TryParseFactory(Guid, ImmutableArray{CustomAttributeData}, out TParsedFactoryDetails?)"/>,
	/// excepted for the fact that it works from serialized details instead of raw custom attributes data.
	/// </para>
	/// <para>The discovery subsystem is responsible for ensuring that all details necessary are properly serialized in <typeparamref name="TParsedFactoryDetails"/>.</para>
	/// </remarks>
	/// <param name="factoryId">The factory ID.</param>
	/// <param name="parsedFactoryDetails">The details that were previously parsed for the factory.</param>
	/// <returns></returns>
	bool TryRegisterFactory(Guid factoryId, TParsedFactoryDetails parsedFactoryDetails);

	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>Indicates that the source must be stopped.</summary>
	/// <remarks>When called, this methods signals that the source must be shut down, generally because of the process being shutdown.</remarks>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	Task StopAsync(CancellationToken cancellationToken);

	ValueTask<TResult?> InvokeFactoryAsync
	(
		TFactory factory,
		ComponentCreationParameters<TKey, TCreationContext> creationParameters,
		CancellationToken cancellationToken
	);
}

public interface IDiscoveryService<TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
	: IDiscoveryService<SimpleComponentFactory<TCreationContext, TResult>, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
	where TKey : notnull, IEquatable<TKey>
	where TParsedFactoryDetails : notnull
	where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
	where TCreationContext : class, IComponentCreationContext
	where TComponent : class, IAsyncDisposable
	where TResult : ComponentCreationResult<TKey, TComponent>
{
	ValueTask<TResult?> IDiscoveryService<SimpleComponentFactory<TCreationContext, TResult>, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>.InvokeFactoryAsync
	(
		SimpleComponentFactory<TCreationContext, TResult> factory,
		ComponentCreationParameters<TKey, TCreationContext> creationParameters,
		CancellationToken cancellationToken
	) => factory(creationParameters.CreationContext!, cancellationToken);
}
