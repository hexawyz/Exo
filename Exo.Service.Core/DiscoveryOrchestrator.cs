using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using Exo.Discovery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Exo.AsyncLock;

namespace Exo.Service;

internal class DiscoveryOrchestrator : IHostedService, IDiscoveryOrchestrator
{
	private delegate ValueTask<object> DriverFactory<TContext>(TContext context, CancellationToken cancellationToken);

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

		public abstract bool RegisterFactory(Guid factoryId, ImmutableArray<CustomAttributeData> attributes, MethodReference methodReference, AssemblyName assemblyName);
		public abstract ValueTask StartAsync(CancellationToken cancellationToken);
	}

	private sealed class DiscoverySource<TDiscoveryService, TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult> : DiscoverySource, IDiscoverySink<TKey, TDiscoveryContext, TCreationContext>
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TFactory : class, Delegate
		where TKey : IEquatable<TKey>
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

		public override bool RegisterFactory(Guid factoryId, ImmutableArray<CustomAttributeData> attributes, MethodReference methodReference, AssemblyName assemblyName)
		{
			bool success;
			try
			{
				success = Service.RegisterFactory(factoryId, attributes);
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
						_states.Remove(context.DiscoveredKeys, state);
						// TODO: Log error with mismatched keys.
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
							return;
						}
						if (!ReferenceEquals(_states.GetOrAdd(key, state), state))
						{
							// TODO: Log ?
							_states.Remove(creationParameters.AssociatedKeys, state);
							return;
						}
					}
				}

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

				// TODO: Still need to handle the additional disposables managed by the creation context.
				// Drivers are supposed to properly dispose of what they use, so it is not a huge problem, but it would be better to guarantee cleanup.
				// Only caveat is that these disposables need to be tied up to the lifetime rather than the state, as we can't know when the driver will stop using the objects.
				// This is especially important for the nested driver registries, that will generally be tied to the lifetime of a driver and not to that of the registration.

				state.AssociatedKeys = result.RegistrationKeys;
				state.Component = result.Component;
				state.Registration = result.DisposableResult;
				state.ComponentReferenceCounter = Orchestrator.ReferenceCounters.GetOrCreateValue(result.Component);

				using (await state.ComponentReferenceCounter.Lock.WaitAsync(default).ConfigureAwait(false))
				{
					state.ComponentReferenceCounter.ReferenceCount++;
				}

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
					await Orchestrator.DriverRegistry.AddDriverAsync(Unsafe.As<Driver>(state.Component)).ConfigureAwait(false);
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
			if (state.ComponentReferenceCounter is not null)
			{
				using (await state.ComponentReferenceCounter.Lock.WaitAsync(default).ConfigureAwait(false))
				{
					if (state.Registration is not null)
					{
						await state.Registration.DisposeAsync().ConfigureAwait(false);
					}

					if (--state.ComponentReferenceCounter.ReferenceCount == 0)
					{
						if (state.Component is { } component)
						{
							if (typeof(TComponent) == typeof(Driver) && shouldUnregisterDriver)
							{
								await Orchestrator.DriverRegistry.RemoveDriverAsync(Unsafe.As<Driver>(component)).ConfigureAwait(false);
							}
							await state.Component.DisposeAsync();
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
		public IAsyncDisposable? Component { get; set; }
		public IAsyncDisposable? Registration { get; set; }
		public ComponentReferenceCounter? ComponentReferenceCounter { get; set; }

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

	private sealed class ComponentReferenceCounter
	{
		public AsyncLock Lock { get; } = new();
		public int ReferenceCount { get; set; }
	}

	private ILogger<DiscoveryOrchestrator> Logger { get; }
	private readonly ConcurrentDictionary<TypeReference, DiscoveryServiceState> _states;
	private IDriverRegistry DriverRegistry { get; }
	private IAssemblyLoader AssemblyLoader { get; }
	private readonly IAssemblyParsedDataCache<DiscoveredAssemblyDetails> _parsedDataCache;
	private readonly ConditionalWeakTable<Type, object> _componentStates;
	private ConditionalWeakTable<Type, ExclusionLock> ExclusionLocks { get; }
	private ConditionalWeakTable<object, ComponentReferenceCounter> ReferenceCounters { get; }
	private readonly CancellationTokenSource _cancellationTokenSource;
	private List<DiscoveryServiceState>? _pendingInitializations;

	public DiscoveryOrchestrator(ILogger<DiscoveryOrchestrator> logger, IDriverRegistry driverRegistry, IAssemblyParsedDataCache<DiscoveredAssemblyDetails> parsedDataCache, IAssemblyLoader assemblyLoader)
	{
		Logger = logger;
		_states = new();
		DriverRegistry = driverRegistry;
		AssemblyLoader = assemblyLoader;
		_parsedDataCache = parsedDataCache;
		_componentStates = new();
		ExclusionLocks = new();
		ReferenceCounters = new();
		_cancellationTokenSource = new();
		_pendingInitializations = new();
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref _pendingInitializations, null) is not { } pendingInitializations)
		{
			throw new InvalidOperationException("The service was already served.");
		}
		// First refresh the assemblies, ensuring everything is up to date
		RefreshAssemblyCache();
		// Then, initialize all the sources, which will ensure the discovery is started once all factories have been registered.
		// This will be enough until we ever decide to support dynamic addition of plugins. (Then, the code will need to be improved to react to factories registered late)
		foreach (var state in pendingInitializations)
		{
			await StartSourceAsync(state, cancellationToken).ConfigureAwait(false);
		}
	}

	public IDiscoverySink<TKey, TDiscoveryContext, TCreationContext> RegisterDiscoveryService<TDiscoveryService, TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>(TDiscoveryService service)
		where TDiscoveryService : class, IDiscoveryService<TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>
		where TFactory : class, Delegate
		where TDiscoveryContext : class, IComponentDiscoveryContext<TKey, TCreationContext>
		where TKey : IEquatable<TKey>
		where TCreationContext : class, IComponentCreationContext
		where TComponent : class, IAsyncDisposable
		where TResult : ComponentCreationResult<TKey, TComponent>
	{
		var state = _states.GetOrAdd(typeof(TDiscoveryService), _ => new());

		DiscoverySource<TDiscoveryService, TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult> source;
		lock (state)
		{
			if (state.Source is not null) throw new InvalidOperationException("A discovery service of the same type has already been registered.");

			state.Source = source = new DiscoverySource<TDiscoveryService, TFactory, TKey, TDiscoveryContext, TCreationContext, TComponent, TResult>(this, state.KnownFactoryMethods, service);

			if (!state.KnownFactoryMethods.IsEmpty)
			{
				RegisterFactories(source, state.KnownFactoryMethods);
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
		lock (state)
		{
			source = state.Source;
		}
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

	private void RegisterFactories(DiscoverySource source, ConcurrentDictionary<Guid, FactoryMethodDetails> factories)
	{
		try
		{
			var assemblies = new Dictionary<AssemblyName, Assembly>();
			foreach (var kvp in factories)
			{
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
				source.RegisterFactory(kvp.Key, [.. method.GetCustomAttributesData()], kvp.Value.MethodReference, kvp.Value.AssemblyName);
			}
		}
		catch (Exception ex)
		{
			// TODO: Log error
		}
	}

	private void RefreshAssemblyCache()
	{
		foreach (var assembly in AssemblyLoader.AvailableAssemblies)
		{
			OnAssemblyAdded(assembly);
		}
	}

	private void OnAssemblyAdded(AssemblyName assemblyName)
	{
		using var context = AssemblyLoader.CreateMetadataLoadContext(assemblyName);
		var assembly = context.LoadFromAssemblyName(assemblyName);

		DiscoveredAssemblyDetails assemblyDetails;
		try
		{
			if (!_parsedDataCache.TryGetValue(assemblyName, out assemblyDetails))
			{
				_parsedDataCache.SetValue(assemblyName, assemblyDetails = ParseAssembly(assembly));
			}
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
					var method = methods.Single(methodDetails.MethodSignature.Matches);
					foreach (var discoverySubsystemType in methodDetails.DiscoverySubsystemTypes)
					{
						var state = _states.GetOrAdd(discoverySubsystemType, _ => new());

						lock (state)
						{
							state.KnownFactoryMethods.TryAdd(methodDetails.Id, new(methodDetails.Id, assemblyName, new MethodReference(typeDetails.Name, method)));
							if (state.Source is { } source)
							{
								source.RegisterFactory(methodDetails.Id, [.. method.GetCustomAttributesData()], new MethodReference(typeDetails.Name, methodDetails.MethodSignature), assembly.GetName());
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
						if (!@interface.MatchesGeneric(typeof(IDiscoveryService<,,,,,>))) continue;

						var arguments = @interface.GetGenericArguments();

						var result = ComponentFactory.Validate(method, arguments[0], arguments[3], arguments[5]);

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
