using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Configuration;
using Exo.Discovery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

internal class DiscoveryOrchestrator : IHostedService, IDiscoveryOrchestrator
{
	private abstract class DiscoverySource
	{
		protected DiscoveryOrchestrator Orchestrator { get; }
		protected ConcurrentDictionary<Guid, FactoryMethodDetails> KnownFactoryMethods { get; }
		public abstract Type Type { get; }

		protected DiscoverySource(DiscoveryOrchestrator orchestrator, ConcurrentDictionary<Guid, FactoryMethodDetails> knownFactoryMethods)
		{
			Orchestrator = orchestrator;
			KnownFactoryMethods = knownFactoryMethods;
		}

		public abstract ValueTask<bool> TryReadAndRegisterFactoryAsync(Guid factoryId, MethodReference methodReference, AssemblyName assemblyName, CancellationToken cancellationToken);
		public abstract ValueTask<bool> TryParseAndRegisterFactoryAsync(Guid factoryId, ImmutableArray<CustomAttributeData> attributes, MethodReference methodReference, AssemblyName assemblyName, CancellationToken cancellationToken);
		public abstract ValueTask StartAsync(CancellationToken cancellationToken);
	}

	private sealed class DiscoverySource<TDiscoveryService, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult> : DiscoverySource, IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TFactory : class, Delegate
		where TKey : IEquatable<TKey>
		where TParsedFactoryDetails : notnull
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>
	{
		private readonly ConcurrentDictionary<TKey, ComponentState<TKey>> _states;
		private readonly AsyncLock _arrivalPreparationLock;

		public DiscoverySource(DiscoveryOrchestrator orchestrator, ConcurrentDictionary<Guid, FactoryMethodDetails> knownFactoryMethods, TDiscoveryService service)
			: base(orchestrator, knownFactoryMethods)
		{
			Service = service;
			_states = Unsafe.As<ConcurrentDictionary<TKey, ComponentState<TKey>>>(Orchestrator._componentStates.GetValue(typeof(TKey), _ => new ConcurrentDictionary<TKey, ComponentState<TKey>>()));
			_arrivalPreparationLock = new();
		}

		public TDiscoveryService Service { get; }
		public override Type Type => typeof(TDiscoveryService);

		public void Dispose()
		{
			// TODO
		}

		public override ValueTask StartAsync(CancellationToken cancellationToken) => Service.StartAsync(cancellationToken);

		public override async ValueTask<bool> TryReadAndRegisterFactoryAsync(Guid factoryId, MethodReference methodReference, AssemblyName assemblyName, CancellationToken cancellationToken)
		{
			TParsedFactoryDetails parsedDetails;

			try
			{
				var readResult = await Orchestrator._factoryConfigurationContainer.ReadValueAsync<TParsedFactoryDetails>(factoryId, cancellationToken).ConfigureAwait(false);
				if (!readResult.Found)
				{
					return false;
				}
				parsedDetails = readResult.Value!;
			}
			catch (Exception ex)
			{
				Orchestrator.Logger.DiscoveryFactoryDetailsWriteError(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName, ex);
				return false;
			}

			bool success;
			try
			{
				success = Service.TryRegisterFactory(factoryId, parsedDetails!);
			}
			catch (Exception ex)
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationError(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName, ex);
				goto Completed;
			}

			if (success)
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationSuccess(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName);
			}
			else
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationFailure(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName);
			}

		Completed:;
			// NB: Perhaps having multiple result states here could be useful, but the idea is that we want to return false only if the value failed to be deserialized.
			// Because the methods here do multiple things at once (read & register or parse & write & register), the intent is not exactly clear, but we want to avoid parsing assemblies for nothing.
			// In this case, returning true indicate that we have run the Register method, regardless of its success.
			return true;
		}

		public override async ValueTask<bool> TryParseAndRegisterFactoryAsync(Guid factoryId, ImmutableArray<CustomAttributeData> attributes, MethodReference methodReference, AssemblyName assemblyName, CancellationToken cancellationToken)
		{
			TParsedFactoryDetails? parsedDetails;

			bool success;
			try
			{
				success = Service.TryParseFactory(attributes, out parsedDetails);
			}
			catch (Exception ex)
			{
				Orchestrator.Logger.DiscoveryFactoryParsingError(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName, ex);
				return false;
			}

			if (success)
			{
				try
				{
					// NB: Should not need the ! here but for some reason it does not makes the link with the value of success ?
					await Orchestrator._factoryConfigurationContainer.WriteValueAsync(factoryId, parsedDetails!, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Orchestrator.Logger.DiscoveryFactoryDetailsWriteError(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName, ex);
					return false;
				}
			}

			try
			{
				success = Service.TryRegisterFactory(factoryId, parsedDetails!);
			}
			catch (Exception ex)
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationError(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName, ex);
				return false;
			}

			if (success)
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationSuccess(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName);
			}
			else
			{
				Orchestrator.Logger.DiscoveryFactoryRegistrationFailure(methodReference.Signature.MethodName, methodReference.TypeName, assemblyName.FullName, Service.FriendlyName);
			}

			return success;
		}

		public async void HandleArrival(TDiscoveryContext context)
		{
			try
			{
				await HandleArrivalAsync(context, Orchestrator._cancellationTokenSource.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// TODO: Log error.
			}
		}

		public async void HandleRemoval(TKey key)
		{
			try
			{
				await HandleRemovalAsync(key, Orchestrator._cancellationTokenSource.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// TODO: Log error.
			}
		}

		private async ValueTask HandleArrivalAsync(TDiscoveryContext context, CancellationToken cancellationToken)
		{
			// Prepare a state object for the component initialization.
			var state = new ComponentState<TKey>(context.DiscoveredKeys);

			using (await state.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var keys = new HashSet<TKey>();
				ComponentCreationParameters<TKey, TCreationContext> creationParameters;
				// Until we got a complete set of keys to work with, we work in exclusivity, in order to avoid conflicts between two operations.
				using (await _arrivalPreparationLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					// Register the state for all keys initially known.
					foreach (var key in context.DiscoveredKeys)
					{
						if (!keys.Add(key))
						{
							// TODO: Log duplicate keys.
							_states.Remove(context.DiscoveredKeys, state);
							return;
						}
						if (!_states.TryAdd(key, state))
						{
							// TODO: Log ?
							_states.Remove(context.DiscoveredKeys, state);
							return;
						}
					}
					try
					{
						creationParameters = await context.PrepareForCreationAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						if (typeof(TComponent) == typeof(Driver))
						{
							Orchestrator.Logger.DiscoveryDriverCreationParametersPreparationFailure(ex);
						}
						else
						{
							Orchestrator.Logger.DiscoveryComponentCreationParametersPreparationFailure(ex);
						}
						_states.Remove(context.DiscoveredKeys, state);
						return;
					}

					// Validate that all initial keys are still present in the returned keys.
					foreach (var key in creationParameters.AssociatedKeys)
					{
						keys.Remove(key);
					}

					if (keys.Count > 0)
					{
						// TODO: Log error with mismatched keys.
						_states.Remove(context.DiscoveredKeys, state);
						await creationParameters.DisposeAsync().ConfigureAwait(false);
						return;
					}

					state.AssociatedKeys = creationParameters.AssociatedKeys;

					// Register the state for the newer keys in the creation parameters.
					foreach (var key in creationParameters.AssociatedKeys)
					{
						if (!keys.Add(key))
						{
							// TODO: Log duplicate keys.
							_states.Remove(creationParameters.AssociatedKeys, state);
							await creationParameters.DisposeAsync().ConfigureAwait(false);
							return;
						}
						if (!ReferenceEquals(_states.GetOrAdd(key, state), state))
						{
							// TODO: Log ?
							_states.Remove(creationParameters.AssociatedKeys, state);
							await creationParameters.DisposeAsync().ConfigureAwait(false);
							return;
						}
					}
				}

				try
				{
					// Now run through the factories to find one that will successfully create the result.
					TResult? result = null;
					ExclusionLock? exclusionLock;
					foreach (var factoryId in creationParameters.FactoryIds)
					{
						if (KnownFactoryMethods.TryGetValue(factoryId, out var details))
						{
							try
							{
								var liveFactoryDetails = details.GetLiveDetails<TFactory, TCreationContext, TResult>(Orchestrator.AssemblyLoader, Orchestrator.ExclusionLocks);
								exclusionLock = liveFactoryDetails.ExclusionLock;

								if (exclusionLock is not null)
								{
									using (await exclusionLock.AcquireForCreationAsync(factoryId, cancellationToken).ConfigureAwait(false))
									{
										result = await Service.InvokeFactoryAsync(liveFactoryDetails.Factory, creationParameters, cancellationToken).ConfigureAwait(false);
									}
								}
								else
								{
									result = await Service.InvokeFactoryAsync(liveFactoryDetails.Factory, creationParameters, cancellationToken).ConfigureAwait(false);
								}
								if (result is not null) break;
							}
							catch (Exception ex)
							{
								if (typeof(TComponent) == typeof(Driver))
								{
									Orchestrator.Logger.DiscoveryDriverCreationFailure(details.MethodReference.Signature.MethodName, details.MethodReference.TypeName, details.AssemblyName.FullName, ex);
								}
								else
								{
									Orchestrator.Logger.DiscoveryComponentCreationFailure(details.MethodReference.Signature.MethodName, details.MethodReference.TypeName, details.AssemblyName.FullName, ex);
								}
								_states.Remove(creationParameters.AssociatedKeys, state);
								return;
							}
						}
					}

					// If no valid result was produced, rollback the state and exit.
					if (result is null)
					{
						// TODO: Log some information. NB: It could be important to know if some factories were called.
						_states.Remove(creationParameters.AssociatedKeys, state);
						return;
					}

					// The shared reference handles the additional disposables managed by the creation context.
					// Drivers are supposed to properly dispose of what they use, but we need to have a strong guarantee that objects will be disposed when needed.
					// These disposables need to be tied up to the lifetime rather than the state, as we can't know when the driver will stop using the objects.
					// This is especially important for the nested driver registries, that will generally be tied to the lifetime of a driver and not to that of the registration.

					var sharedReference = Orchestrator.ComponentReferences.GetOrCreateValue(result.Component);

					var disposableDependencies = creationParameters.CreationContext.CollectDisposableDependencies();
					try
					{
						await sharedReference.AddReferenceAsync(result.Component, disposableDependencies, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						Orchestrator.Logger.DiscoveryComponentAddSharedReferenceFailure(ex);
						foreach (var dependency in disposableDependencies)
						{
							try
							{
								await dependency.DisposeAsync().ConfigureAwait(false);
							}
							catch (Exception ex2)
							{
								Orchestrator.Logger.DiscoveryComponentDependencyDisposalFailure(ex2);
							}
						}
						return;
					}

					state.AssociatedKeys = result.RegistrationKeys;
					state.Registration = result.DisposableResult;
					state.SharedComponentReference = sharedReference;

					if (typeof(TComponent) == typeof(Driver))
					{
						var driver = Unsafe.As<Driver>(result.Component);
						Orchestrator.Logger.DiscoveryDriverCreationSuccess(driver.FriendlyName, driver.ConfigurationKey.DeviceMainId);
					}
					else
					{
						var component = result.Component as Component;
						if (component is not null)
						{
							Orchestrator.Logger.DiscoveryComponentCreationSuccess(component.FriendlyName);
						}
					}

					// Determine which keys need to be removed.
					// The factory is allowed to register itself on only a subset of the keys.
					// It is generally not advised, but it might be necessary in very specific scenarios.
					foreach (var key in result.RegistrationKeys)
					{
						keys.Remove(key);
					}

					// Unregister the state from all remaining keys.
					_states.Remove(keys, state);

					// And finally register with all the final keys.
					foreach (var key in result.RegistrationKeys)
					{
						if (!keys.Add(key))
						{
							// TODO: Log duplicate keys.
							_states.Remove(result.RegistrationKeys, state);
							await DisposeStateAsync(state, false, default).ConfigureAwait(false);
							return;
						}
						if (!ReferenceEquals(_states.GetOrAdd(key, state), state))
						{
							// TODO: Log ?
							_states.Remove(result.RegistrationKeys, state);
							await DisposeStateAsync(state, false, default).ConfigureAwait(false);
							return;
						}
					}

					if (typeof(TComponent) == typeof(Driver))
					{
						try
						{
							await Orchestrator.DriverRegistry.AddDriverAsync(Unsafe.As<Driver>(result.Component)).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							// NB: This should generally not happen, but it seems that there is a problem in case some devices changing their serial number.
							// This case should be handled properly, and then we can remove the catch clause.
							// The specific problem was noticed with the LIGHTSPEED receiver (some time after a driver and/or firmware update, it might be related)
							// The device sometimes changes its "serial number" between two successive connections to the computer, which is weird.
							// One way to address this is to remove the serial number from the device key, but it would be better to avoid this if possible.
							// Whatever happens after, the DriverRegistry.AddDriverAsync should not crash in that case.
							// (Exception is "The same main device name was found for devices")
							Orchestrator.Logger.LogError(ex, "Problem when adding the driver to the registry.");
							await DisposeStateAsync(state, false, cancellationToken);
						}
					}
				}
				finally
				{
					await creationParameters.DisposeAsync().ConfigureAwait(false);
				}
			}
		}

		public async ValueTask HandleRemovalAsync(TKey key, CancellationToken cancellationToken)
		{
			if (!_states.TryGetValue(key, out var state)) return;

			using (await state.Lock.WaitAsync(cancellationToken))
			{
				_states.Remove(state.AssociatedKeys, state);

				await DisposeStateAsync(state, true, cancellationToken).ConfigureAwait(false);
			}
		}

		public async ValueTask DisposeStateAsync(ComponentState<TKey> state, bool shouldUnregisterDriver, CancellationToken cancellationToken)
		{
			if (state.SharedComponentReference is { } sharedReference)
			{
				if (state.Registration is not null)
				{
					await state.Registration.DisposeAsync().ConfigureAwait(false);
				}

				var (component, dependencies) = await sharedReference.RemoveReferenceAsync(cancellationToken).ConfigureAwait(false);
				if (component is not null)
				{
					if (typeof(TComponent) == typeof(Driver) && shouldUnregisterDriver)
					{
						await Orchestrator.DriverRegistry.RemoveDriverAsync(Unsafe.As<Driver>(component)).ConfigureAwait(false);
					}
					try
					{
						await component.DisposeAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						var type = component.GetType();
						if (typeof(TComponent) == typeof(Driver))
						{
							Orchestrator.Logger.DiscoveryDriverDisposalFailure(type.FullName!, type.Assembly.FullName!, ex);
						}
						else
						{
							Orchestrator.Logger.DiscoveryComponentDisposalFailure(type.FullName!, type.Assembly.FullName!, ex);
						}
					}
					foreach (var dependency in dependencies)
					{
						try
						{
							await dependency.DisposeAsync().ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							Orchestrator.Logger.DiscoveryComponentDependencyDisposalFailure(ex);
						}
					}
				}
			}
		}
	}

	private sealed class ComponentState<TKey>
		where TKey : notnull, IEquatable<TKey>
	{
		public AsyncLock Lock { get; }
		public ImmutableArray<TKey> AssociatedKeys { get; set; }
		public IAsyncDisposable? Registration { get; set; }
		public ComponentSharedReference? SharedComponentReference { get; set; }

		public ComponentState(ImmutableArray<TKey> associatedKeys)
		{
			Lock = new();
			AssociatedKeys = associatedKeys;
		}
	}

	private sealed class DiscoveryServiceState
	{
		// Identifies the currently active source for this service.
		public DiscoverySource? Source { get; set; }
		// Identifies all the known factory methods for the service.
		// If the source is active, they must all have been registered with it. Otherwise they are kept there for when the source is registered, if it ever is.
		// Generally, all the discovery sources are going to be registered, but we can't guarantee the order of operations.
		public ConcurrentDictionary<Guid, FactoryMethodDetails> KnownFactoryMethods { get; } = new();
		// A lock used to access the state.
		public AsyncLock Lock { get; } = new();
	}

	private sealed class FactoryMethodDetails
	{
		public Guid FactoryId { get; }
		public AssemblyName AssemblyName { get; }
		public MethodReference MethodReference { get; }
		private DependentHandle _dependentHandle;

		public FactoryMethodDetails(Guid factoryId, AssemblyName assemblyName, MethodReference methodReference)
		{
			FactoryId = factoryId;
			AssemblyName = assemblyName;
			MethodReference = methodReference;
		}

		public LiveFactoryMethodDetails<TFactory> GetLiveDetails<TFactory, TCreationContext, TResult>(IAssemblyLoader assemblyLoader, ConditionalWeakTable<Type, ExclusionLock> exclusionLocks)
			where TFactory : class, Delegate
			where TCreationContext : class, IComponentCreationContext
			where TResult : class
		{
			lock (this)
			{
				if (_dependentHandle.IsAllocated)
				{
					var (target, dependent) = _dependentHandle.TargetAndDependent;

					if (dependent is not null) return Unsafe.As<LiveFactoryMethodDetails<TFactory>>(dependent);

					_dependentHandle.Dispose();
				}

				var method = GetMethod(assemblyLoader, AssemblyName, MethodReference);

				var exclusionLock = method.GetCustomAttribute<ExclusionCategoryAttribute>() is { } exclusionCategory ?
					exclusionLocks.GetOrCreateValue(exclusionCategory.Category) :
					null;

				var factory = ComponentFactory.Get<TFactory, TCreationContext, TResult>(method);

				var details = new LiveFactoryMethodDetails<TFactory>(factory, exclusionLock);

				_dependentHandle = new(method, details);

				return details;
			}
		}

		private static MethodInfo GetMethod(IAssemblyLoader assemblyLoader, AssemblyName assemblyName, MethodReference methodReference)
		{
			var assembly = assemblyLoader.LoadAssembly(assemblyName);
			var type = assembly.GetType(methodReference.TypeName) ?? throw new InvalidOperationException($"The type {methodReference.TypeName} was not found in {assemblyName}.");
			var signature = methodReference.Signature;
			return type.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => signature.Matches(m)) ??
				throw new InvalidOperationException($"Could not find method with signature {signature} in {methodReference.TypeName} of {assemblyName}.");
		}
	}

	private sealed class LiveFactoryMethodDetails<TFactory>
	{
		public TFactory Factory { get; }
		public ExclusionLock? ExclusionLock { get; }

		public LiveFactoryMethodDetails(TFactory factory, ExclusionLock? exclusionLock)
		{
			Factory = factory;
			ExclusionLock = exclusionLock;
		}
	}

	// This implements the exclusion locks.
	// Basic idea is that we want to maintain at least some amounts of parallelism by allowing two instances of the same factory to run simultaneously.
	// However, we want to prevent two distinct factories from running at the same time.
	// And a component should never be disposed while any factory is run.
	// As such, it follows that at most one factory can be running at a given time, in one or multiple instances.
	// Additionally, we should be able to maximize parallelism by allowing reordering of any operations between two destructions.
	// The minimum valid implementation of this is a full exclusive lock, which will prevent any form of parallelism. Not ideal, but quite simple.
	// TODO: Implement the better version that allows for parallelism.
	private sealed class ExclusionLock
	{
		private readonly AsyncLock _mainLock = new();

		public ValueTask<AsyncLock.Registration> AcquireForCreationAsync(Guid factory, CancellationToken cancellationToken)
			=> _mainLock.WaitAsync(cancellationToken);

		public ValueTask<AsyncLock.Registration> AcquireForDestructionAsync(CancellationToken cancellationToken)
			=> _mainLock.WaitAsync(cancellationToken);
	}

	private sealed class ComponentSharedReference
	{
		private readonly AsyncLock _lock = new();
		private IAsyncDisposable? _component;
		private ImmutableArray<IAsyncDisposable> _disposableDependencies = [];
		private int _referenceCount;
		public IAsyncDisposable? Component => _component;

		public async ValueTask AddReferenceAsync(IAsyncDisposable component, ImmutableArray<IAsyncDisposable> dependencies, CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_component is null)
				{
					_component = component;
				}
				else if (!ReferenceEquals(_component, component))
				{
					throw new InvalidOperationException("The component reference does not match.");
				}
				_referenceCount++;
			}
		}

		public async ValueTask<(IAsyncDisposable? Component, ImmutableArray<IAsyncDisposable> Dependencies)> RemoveReferenceAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (--_referenceCount == 0)
				{
					return (Interlocked.Exchange(ref _component, null), ImmutableInterlocked.InterlockedExchange(ref _disposableDependencies, []));
				}
				return default;
			}
		}

		private void AddDependencies(ImmutableArray<IAsyncDisposable> dependencies)
		{
			if (dependencies.IsDefaultOrEmpty) return;

			var disposableDependencies = _disposableDependencies;
			if (disposableDependencies.IsEmpty)
			{
				disposableDependencies = dependencies;
			}
			else
			{
				_disposableDependencies = disposableDependencies.AddRange(dependencies);
			}
		}
	}

	private ILogger<DiscoveryOrchestrator> Logger { get; }
	private readonly ConcurrentDictionary<TypeReference, DiscoveryServiceState> _states;
	private IDriverRegistry DriverRegistry { get; }
	private IAssemblyLoader AssemblyLoader { get; }
	private readonly IAssemblyParsedDataCache<DiscoveredAssemblyDetails> _parsedDataCache;
	private readonly ConditionalWeakTable<Type, object> _componentStates;
	private ConditionalWeakTable<Type, ExclusionLock> ExclusionLocks { get; }
	private ConditionalWeakTable<object, ComponentSharedReference> ComponentReferences { get; }
	private readonly CancellationTokenSource _cancellationTokenSource;
	private List<DiscoveryServiceState>? _pendingInitializations;
	private readonly IConfigurationContainer<Guid> _factoryConfigurationContainer;

	public DiscoveryOrchestrator
	(
		ILogger<DiscoveryOrchestrator> logger,
		IDriverRegistry driverRegistry,
		IAssemblyParsedDataCache<DiscoveredAssemblyDetails> parsedDataCache,
		IAssemblyLoader assemblyLoader,
		IConfigurationContainer<Guid> factoryConfigurationContainer
	)
	{
		Logger = logger;
		_states = new();
		DriverRegistry = driverRegistry;
		AssemblyLoader = assemblyLoader;
		_parsedDataCache = parsedDataCache;
		_componentStates = new();
		ExclusionLocks = new();
		ComponentReferences = new();
		_cancellationTokenSource = new();
		_pendingInitializations = new();
		_factoryConfigurationContainer = factoryConfigurationContainer;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref _pendingInitializations, null) is not { } pendingInitializations)
		{
			throw new InvalidOperationException("The service was already served.");
		}
		// First refresh the assemblies, ensuring everything is up to date
		await RefreshAssemblyCacheAsync().ConfigureAwait(false);
		// Then, initialize all the sources, which will ensure the discovery is started once all factories have been registered.
		// This will be enough until we ever decide to support dynamic addition of plugins. (Then, the code will need to be improved to react to factories registered late)
		foreach (var state in pendingInitializations)
		{
			await StartSourceAsync(state, cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask<IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>> RegisterDiscoveryServiceAsync<TDiscoveryService, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>(TDiscoveryService service)
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TFactory : class, Delegate
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TKey : IEquatable<TKey>
		where TParsedFactoryDetails : notnull
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>
	{
		// Validate that the parsed factory details have a GUID applied that will allow them to be serialized.
		// It is better to validate this upfront, as it is easy to do and will avoid starting too much stuff.
		_ = TypeId.Get<TParsedFactoryDetails>();

		var state = _states.GetOrAdd(typeof(TDiscoveryService), _ => new());

		DiscoverySource<TDiscoveryService, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult> source;
		using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			if (state.Source is not null) throw new InvalidOperationException("A discovery service of the same type has already been registered.");

			state.Source = source = new DiscoverySource<TDiscoveryService, TFactory, TKey, TParsedFactoryDetails, TDiscoveryContext, TCreationContext, TComponent, TResult>(this, state.KnownFactoryMethods, service);

			if (!state.KnownFactoryMethods.IsEmpty)
			{
				await RegisterFactoriesAsync(source, state.KnownFactoryMethods, default).ConfigureAwait(false);
			}

			var pendingInitializations = Volatile.Read(ref _pendingInitializations);
			if (pendingInitializations is not null)
			{
				lock (pendingInitializations)
				{
					if (Volatile.Read(ref _pendingInitializations) is not null)
					{
						pendingInitializations.Add(state);
						goto Completed;
					}
				}
			}
			_ = Task.Run(() => StartSourceAsync(state, CancellationToken.None));
		}
	Completed:;
		return source;
	}

	private async Task StartSourceAsync(DiscoveryServiceState state, CancellationToken cancellationToken)
	{
		DiscoverySource? source;
		using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
		{
			source = state.Source;
			if (source is null) return;
			try
			{
				await source.StartAsync(cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				// TODO: Log.
				// TODO: Dispose the source?
			}
		}
	}

	private async ValueTask RegisterFactoriesAsync(DiscoverySource source, ConcurrentDictionary<Guid, FactoryMethodDetails> factories, CancellationToken cancellationToken)
	{
		try
		{
			var assemblies = new Dictionary<AssemblyName, Assembly>();
			foreach (var kvp in factories)
			{
				// Avoid parsing assemblies if we can avoid it.
				if (await source.TryReadAndRegisterFactoryAsync(kvp.Key, kvp.Value.MethodReference, kvp.Value.AssemblyName, cancellationToken).ConfigureAwait(false))
				{
					continue;
				}

				if (!assemblies.TryGetValue(kvp.Value.AssemblyName, out var assembly))
				{
					assemblies.Add(kvp.Value.AssemblyName, assembly = AssemblyLoader.CreateMetadataLoadContext(kvp.Value.AssemblyName).LoadFromAssemblyName(kvp.Value.AssemblyName));
				}
				var type = assembly.GetType(kvp.Value.MethodReference.TypeName);
				if (type is null)
				{
					// TODO: Log
					continue;
				}
				var signature = kvp.Value.MethodReference.Signature;
				var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => signature.Matches(m));
				if (method is null)
				{
					// TODO: Log
					continue;
				}
				await source.TryParseAndRegisterFactoryAsync(kvp.Key, [.. method.GetCustomAttributesData()], kvp.Value.MethodReference, kvp.Value.AssemblyName, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			// TODO: Log error
		}
	}

	private async ValueTask RefreshAssemblyCacheAsync()
	{
		foreach (var assembly in AssemblyLoader.AvailableAssemblies)
		{
			await OnAssemblyAdded(assembly).ConfigureAwait(false);
		}
	}

	private async ValueTask OnAssemblyAdded(AssemblyName assemblyName)
	{
		if (_parsedDataCache.TryGetValue(assemblyName, out var assemblyDetails))
		{
			// NB: This is mostly duplicated from the code at the end of the method.
			// TODO: There might be a way to re-merge both of those. (Ideally also with RegisterFactoriesAsync)
			// Probably want to reuse the code that lazily loads assemblies based on the needs of the methods to register.
			// In the code just below, the methods are not registered if they were not parsed previously, so we might end up in weird places if some configuration files are missing.
			// While assuming that methods will have been parsed when the assembly was first parsed is a generally safe assumption,
			// it means that the whole parsing caches need to be deleted for a method to be reparsed, which is less than great in case some files were inadvertently deleted.
			if (assemblyDetails.Types.Length > 0)
			{
				foreach (var typeDetails in assemblyDetails.Types)
				{
					foreach (var methodDetails in typeDetails.FactoryMethods)
					{
						var methodReference = new MethodReference(typeDetails.Name, methodDetails.MethodSignature);

						foreach (var discoverySubsystemType in methodDetails.DiscoverySubsystemTypes)
						{
							var state = _states.GetOrAdd(discoverySubsystemType, _ => new());

							using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
							{
								state.KnownFactoryMethods.TryAdd(methodDetails.Id, new(methodDetails.Id, assemblyName, methodReference));
								if (state.Source is { } source)
								{
									await source.TryReadAndRegisterFactoryAsync
									(
										methodDetails.Id,
										methodReference,
										assemblyName,
										default
									).ConfigureAwait(false);
								}
							}
						}
					}
				}
			}
			return;
		}

		using var context = AssemblyLoader.CreateMetadataLoadContext(assemblyName);
		var assembly = context.LoadFromAssemblyName(assemblyName);

		try
		{
			_parsedDataCache.SetValue(assemblyName, assemblyDetails = ParseAssembly(assembly));
		}
		catch (Exception ex)
		{
			Logger.DiscoveryAssemblyParsingFailure(assemblyName.FullName, ex);
			return;
		}

		if (assemblyDetails.Types.Length > 0)
		{
			foreach (var typeDetails in assemblyDetails.Types)
			{
				var type = assembly.GetType(typeDetails.Name)!;
				var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
				foreach (var methodDetails in typeDetails.FactoryMethods)
				{
					var methodReference = new MethodReference(typeDetails.Name, methodDetails.MethodSignature);
					var method = methods.Single(methodDetails.MethodSignature.Matches);
					foreach (var discoverySubsystemType in methodDetails.DiscoverySubsystemTypes)
					{
						var state = _states.GetOrAdd(discoverySubsystemType, _ => new());

						using (await state.Lock.WaitAsync(default).ConfigureAwait(false))
						{
							state.KnownFactoryMethods.TryAdd(methodDetails.Id, new(methodDetails.Id, assemblyName, methodReference));
							if (state.Source is { } source)
							{
								await source.TryParseAndRegisterFactoryAsync
								(
									methodDetails.Id,
									[.. method.GetCustomAttributesData()],
									methodReference,
									assembly.GetName(),
									default
								).ConfigureAwait(false);
							}
						}
					}
				}
			}
		}
	}

	private DiscoveredAssemblyDetails ParseAssembly(AssemblyName assemblyName)
	{
		using var context = AssemblyLoader.CreateMetadataLoadContext(assemblyName);
		var assembly = context.LoadFromAssemblyName(assemblyName);
		return ParseAssembly(assembly);
	}

	private static DiscoveredAssemblyDetails ParseAssembly(Assembly assembly)
	{
		var factoryMethods = ImmutableArray.CreateBuilder<DiscoveredTypeDetails>();
		var typeFactoryMethods = ImmutableArray.CreateBuilder<DiscoveredFactoryMethodDetails>();
		var discoverySubsystems = ImmutableArray.CreateBuilder<TypeReference>();
		foreach (var type in assembly.DefinedTypes)
		{
			if (!type.IsPublic || type.IsGenericTypeDefinition) continue;

			typeFactoryMethods.Clear();
			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				// First, validate that the method returns a Task or ValueTask.
				if (!method.ReturnType.IsGenericType ||
					method.ReturnType.GetGenericTypeDefinition() is var genericReturnType && !(genericReturnType.Matches(typeof(ValueTask<>)) || genericReturnType.Matches(typeof(Task<>))))
				{
					continue;
				}

				// Retrieve all attributes in order to identify proper factory methods and parse them as needed.
				var attributes = method.GetCustomAttributesData();

				// Find all the discovery subsystems declared for a factory method.
				discoverySubsystems.Clear();
				foreach (var attribute in attributes)
				{
					if (!attribute.AttributeType.MatchesGeneric(typeof(DiscoverySubsystemAttribute<>))) continue;

					var discoverySubsystem = attribute.AttributeType.GetGenericArguments()[0];

					foreach (var @interface in discoverySubsystem.GetInterfaces())
					{
						if (!@interface.MatchesGeneric(typeof(IDiscoveryService<,,,,,,>))) continue;

						var arguments = @interface.GetGenericArguments();

						var result = ComponentFactory.Validate(method, arguments[0], arguments[4], arguments[6]);

						if (result.ErrorCode != ComponentFactory.ValidationErrorCode.None)
						{
							// TODO: Log error instead of throwing. (Must mention that the check failed against the specific subsystem that was requested.)
							result.ThrowIfFailed();
							break;
						}

						discoverySubsystems.Add(discoverySubsystem);
						break;
					}
				}

				// Ignore methods that don't have a discovery subsystem.
				if (discoverySubsystems.Count == 0) continue;

				typeFactoryMethods.Add(new(Guid.NewGuid(), method, discoverySubsystems.DrainToImmutable()));
			}

			// Ignore types that don't have any factory method.
			if (typeFactoryMethods.Count == 0) continue;

			factoryMethods.Add(new(type.FullName!, typeFactoryMethods.DrainToImmutable()));
		}

		return new(factoryMethods.DrainToImmutable());
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
