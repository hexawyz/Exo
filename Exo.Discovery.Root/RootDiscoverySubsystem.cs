using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public class RootDiscoverySubsystem :
	IDiscoveryService<RootComponentKey, RootComponentDiscoveryContext, RootComponentCreationContext, Component, RootComponentCreationResult>,
	IAsyncDisposable
{
	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	internal IDiscoveryOrchestrator DiscoveryOrchestrator { get; }
	internal IDeviceNotificationService DeviceNotificationService { get; }

	internal ConcurrentDictionary<RootComponentKey, Guid> RegisteredFactories { get; }
	private readonly IDiscoverySink<RootComponentKey, RootComponentDiscoveryContext, RootComponentCreationContext> _discoverySink;
	private List<RootComponentKey>? _pendingKeys;

	public string FriendlyName => "Root component discovery";

	public RootDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		DiscoveryOrchestrator = discoveryOrchestrator;
		DeviceNotificationService = deviceNotificationService;
		RegisteredFactories = new();
		_pendingKeys = new();
		_discoverySink = discoveryOrchestrator.RegisterDiscoveryService
		<
			RootDiscoverySubsystem,
			RootComponentKey,
			RootComponentDiscoveryContext,
			RootComponentCreationContext,
			Component,
			RootComponentCreationResult
		>(this);
	}

	public ValueTask DisposeAsync()
	{
		_discoverySink.Dispose();
		return ValueTask.CompletedTask;
	}

	public bool RegisterFactory(Guid factoryId, ImmutableArray<CustomAttributeData> attributes)
	{
		if (attributes.FirstOrDefault<RootComponentAttribute>() is { } attribute && attribute.ConstructorArguments[0].Value is Type key && RegisteredFactories.TryAdd(key, factoryId))
		{
			var pendingKeys = Volatile.Read(ref _pendingKeys);
			if (pendingKeys is not null)
			{
				lock (pendingKeys)
				{
					if (Volatile.Read(ref _pendingKeys) is not null)
					{
						pendingKeys.Add(key);
						return true;
					}
				}
			}
			_discoverySink.HandleArrival(new(this, key));
			return true;
		}
		return false;
	}

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _pendingKeys) is not { } pendingKeys) goto Failed;

		lock (pendingKeys)
		{
			if (Interlocked.Exchange(ref _pendingKeys, null) is null) goto Failed;
		}

		foreach (var key in pendingKeys)
		{
			_discoverySink.HandleArrival(new(this, key));
		}

		return ValueTask.CompletedTask;

	Failed:;
		return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The service ahs already been initialized.")));
	}
}
