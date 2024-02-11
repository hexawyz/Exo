using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Exo.Discovery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

		public void Dispose() => throw new NotImplementedException();

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
						// TODO: Log exception.
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
				foreach (var factoryId in creationParameters.FactoryIds)
				{
					if (KnownFactoryMethods.TryGetValue(factoryId, out var details))
					{
						try
						{
							var factory = details.GetValue<TFactory, TCreationContext, TResult>(Orchestrator.AssemblyLoader);
							result = await Service.InvokeFactoryAsync(factory, creationParameters, cancellationToken).ConfigureAwait(false);
							if (result is null) continue;
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
						if (result.DisposableResult is not null) await result.DisposableResult.DisposeAsync().ConfigureAwait(false);
						else await result.Component.DisposeAsync();
						return;
					}
					if (!ReferenceEquals(_states.GetOrAdd(key, state), state))
					{
						// TODO: Log ?
						_states.Remove(result.RegistrationKeys, state);
						if (result.DisposableResult is not null) await result.DisposableResult.DisposeAsync().ConfigureAwait(false);
						else await result.Component.DisposeAsync();
						return;
					}
				}

				// TODO: Implement the reference counting for multi-registration components.
				state.AssociatedKeys = result.RegistrationKeys;
				state.Component = result.Component;
				state.Registration = result.DisposableResult;

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

				if (state.Registration is not null) await state.Registration.DisposeAsync();
				else if (state.Component is { } component)
				{
					if (typeof(TComponent) == typeof(Driver))
					{
						await Orchestrator.DriverRegistry.RemoveDriverAsync(Unsafe.As<Driver>(component)).ConfigureAwait(false);
					}
					await state.Component.DisposeAsync();
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

	private sealed class FactoryMethodDetails : WeakReference
	{
		public Guid FactoryId { get; }
		public AssemblyName AssemblyName { get; }
		public MethodReference MethodReference { get; }

		public FactoryMethodDetails(Guid factoryId, AssemblyName assemblyName, MethodReference methodReference) : base(null, false)
		{
			FactoryId = factoryId;
			AssemblyName = assemblyName;
			MethodReference = methodReference;
		}

		public TFactory GetValue<TFactory, TCreationContext, TResult>(IAssemblyLoader assemblyLoader)
			where TFactory : class, Delegate
			where TCreationContext : class, IComponentCreationContext
			where TResult : class
		{
			TFactory? value;

			if ((value = Target as TFactory) is not null) return value;
			lock (this)
			{
				if ((value = Target as TFactory) is not null) return value;

				value = ComponentFactory.Get<TFactory, TCreationContext, TResult>(GetMethod(assemblyLoader, AssemblyName, MethodReference));
				Target = value;
				return value;
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

	// TODO
	private sealed class ReferenceCountedLifetime
	{
		//private nint _referenceCount;
	}

	private ILogger<DiscoveryOrchestrator> Logger { get; }
	private readonly ConcurrentDictionary<TypeReference, DiscoveryServiceState> _states;
	private IDriverRegistry DriverRegistry { get; }
	private IAssemblyLoader AssemblyLoader { get; }
	private readonly IAssemblyParsedDataCache<DiscoveredAssemblyDetails> _parsedDataCache;
	private readonly ConditionalWeakTable<Type, object> _componentStates;
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

		DiscoveredAssemblyDetails details;
		try
		{
			if (!_parsedDataCache.TryGetValue(assemblyName, out details))
			{
				_parsedDataCache.SetValue(assemblyName, details = ParseAssembly(assembly));
			}
		}
		catch (Exception ex)
		{
			Logger.DiscoveryAssemblyParsingFailure(assemblyName.FullName, ex);
			return;
		}

		if (details.FactoryMethods.Length > 0)
		{
			foreach (var (typeName, factoryMethods) in details.FactoryMethods)
			{
				var type = assembly.GetType(typeName)!;
				var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
				foreach (var (id, methodSignature, discoverySubsystemTypes) in factoryMethods)
				{
					var method = methods.Single(methodSignature.Matches);
					foreach (var discoverySubsystemType in discoverySubsystemTypes)
					{
						var state = _states.GetOrAdd(discoverySubsystemType, _ => new());

						lock (state)
						{
							state.KnownFactoryMethods.TryAdd(id, new(id, assemblyName, new MethodReference(typeName, method)));
							if (state.Source is { } source)
							{
								source.RegisterFactory(id, [.. method.GetCustomAttributesData()], new MethodReference(typeName, methodSignature), assembly.GetName());
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
		var factoryMethods = ImmutableArray.CreateBuilder<(string, ImmutableArray<(Guid, MethodSignature, ImmutableArray<TypeReference>)>)>();
		var typeFactoryMethods = ImmutableArray.CreateBuilder<(Guid, MethodSignature, ImmutableArray<TypeReference>)>();
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

				typeFactoryMethods.Add((Guid.NewGuid(), method, discoverySubsystems.DrainToImmutable()));
			}

			// Ignore types that don't have any factory method.
			if (typeFactoryMethods.Count == 0) continue;

			factoryMethods.Add((type.FullName!, typeFactoryMethods.DrainToImmutable()));
		}

		return new(factoryMethods.DrainToImmutable());
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
