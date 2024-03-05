using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Exo.Configuration;
using Exo.I2C;
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
	internal II2CBusProvider I2CBusProvider { get; }
	internal IConfigurationContainer<Guid> DiscoveryConfigurationContainer { get; }

	internal ConcurrentDictionary<RootComponentKey, Guid> RegisteredFactories { get; }
	private readonly IDiscoverySink<RootComponentKey, RootComponentDiscoveryContext, RootComponentCreationContext> _discoverySink;
	private List<(RootComponentKey Key, Guid TypeId)>? _pendingArrivals;

	public string FriendlyName => "Root component discovery";

	public RootDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService,
		II2CBusProvider i2cBusProvider,
		IConfigurationContainer<Guid> discoveryConfigurationContainer
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		DiscoveryOrchestrator = discoveryOrchestrator;
		DeviceNotificationService = deviceNotificationService;
		I2CBusProvider = i2cBusProvider;
		DiscoveryConfigurationContainer = discoveryConfigurationContainer;
		RegisteredFactories = new();
		_pendingArrivals = new();
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
			var typeId = TryGetTypeId(key);
			var pendingArrivals = Volatile.Read(ref _pendingArrivals);
			if (pendingArrivals is not null)
			{
				lock (pendingArrivals)
				{
					if (Volatile.Read(ref _pendingArrivals) is not null)
					{
						pendingArrivals.Add((key, typeId));
						return true;
					}
				}
			}
			_discoverySink.HandleArrival(new(this, key, typeId));
			return true;
		}
		return false;
	}

	private static Guid TryGetTypeId(Type type)
	{
		foreach (var attribute in type.GetCustomAttributesData())
		{
			if (attribute.Matches<TypeIdAttribute>() && attribute.ConstructorArguments is { Count: 11 } args)
			{
				return new Guid
				(
					(uint)args[0].Value!,
					(ushort)args[1].Value!,
					(ushort)args[2].Value!,
					(byte)args[3].Value!,
					(byte)args[4].Value!,
					(byte)args[5].Value!,
					(byte)args[6].Value!,
					(byte)args[7].Value!,
					(byte)args[8].Value!,
					(byte)args[9].Value!,
					(byte)args[10].Value!
				);
			}
		}
		return default;
	}

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _pendingArrivals) is not { } pendingArrival) goto Failed;

		lock (pendingArrival)
		{
			if (Interlocked.Exchange(ref _pendingArrivals, null) is null) goto Failed;
		}

		foreach (var (key, typeId) in pendingArrival)
		{
			_discoverySink.HandleArrival(new(this, key, typeId));
		}

		return ValueTask.CompletedTask;

	Failed:;
		return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The service ahs already been initialized.")));
	}
}
