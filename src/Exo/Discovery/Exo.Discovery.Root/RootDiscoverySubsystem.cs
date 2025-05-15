using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization.Metadata;
using Exo.Features;
using Exo.I2C;
using Exo.Services;
using Exo.SystemManagementBus;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public class RootDiscoverySubsystem : DiscoveryService<RootDiscoverySubsystem, RootComponentKey, RootFactoryDetails, RootComponentDiscoveryContext, RootComponentCreationContext, Component, RootComponentCreationResult>, IJsonTypeInfoProvider<RootFactoryDetails>
{
	static JsonTypeInfo<RootFactoryDetails> IJsonTypeInfoProvider<RootFactoryDetails>.JsonTypeInfo => SourceGenerationContext.Default.RootFactoryDetails;

	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	internal IDiscoveryOrchestrator DiscoveryOrchestrator { get; }
	internal IDeviceNotificationService DeviceNotificationService { get; }
	internal IPowerNotificationService PowerNotificationService { get; }
	internal II2cBusProvider I2CBusProvider { get; }
	internal ISystemManagementBusProvider SystemManagementBusProvider { get; }
	internal Func<string, IDisplayAdapterI2cBusProviderFeature> FallbackI2cBusProviderFeatureProvider { get; }
	internal ConcurrentDictionary<RootComponentKey, Guid> RegisteredFactories { get; }
	private List<(RootComponentKey Key, Guid TypeId)>? _pendingArrivals;

	public override string FriendlyName => "Root component discovery";

	public static async ValueTask<RootDiscoverySubsystem> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService,
		IPowerNotificationService powerNotificationService,
		II2cBusProvider i2cBusProvider,
		ISystemManagementBusProvider systemManagementBusProvider,
		Func<string, IDisplayAdapterI2cBusProviderFeature> fallbackI2cBusProviderFeatureProvider
	)
	{
		var service = new RootDiscoverySubsystem(loggerFactory, driverRegistry, discoveryOrchestrator, deviceNotificationService, powerNotificationService, i2cBusProvider, systemManagementBusProvider, fallbackI2cBusProviderFeatureProvider);
		try
		{
			await service.RegisterAsync(discoveryOrchestrator);
		}
		catch
		{
			await service.DisposeAsync();
			throw;
		}
		return service;
	}

	private RootDiscoverySubsystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator,
		IDeviceNotificationService deviceNotificationService,
		IPowerNotificationService powerNotificationService,
		II2cBusProvider i2cBusProvider,
		ISystemManagementBusProvider systemManagementBusProvider,
		Func<string, IDisplayAdapterI2cBusProviderFeature> fallbackI2cBusProviderFeatureProvider
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		DiscoveryOrchestrator = discoveryOrchestrator;
		DeviceNotificationService = deviceNotificationService;
		I2CBusProvider = i2cBusProvider;
		SystemManagementBusProvider = systemManagementBusProvider;
		PowerNotificationService = powerNotificationService;
		RegisteredFactories = new();
		_pendingArrivals = new();
		FallbackI2cBusProviderFeatureProvider = fallbackI2cBusProviderFeatureProvider;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out RootFactoryDetails parsedFactoryDetails)
	{
		if (attributes.FirstOrDefault<RootComponentAttribute>() is { } attribute && attribute.ConstructorArguments[0].Value is Type key)
		{
			var typeId = TryGetTypeId(key);
			parsedFactoryDetails = new()
			{
				TypeName = key.AssemblyQualifiedName ?? throw new InvalidOperationException(),
				TypeId = typeId != default ? typeId : null,
			};
			return true;
		}
		parsedFactoryDetails = default;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, RootFactoryDetails parsedFactoryDetails)
	{
		var typeName = parsedFactoryDetails.TypeName;
		var key = Unsafe.As<string, RootComponentKey>(ref typeName);
		var typeId = parsedFactoryDetails.TypeId.GetValueOrDefault();
		if (RegisteredFactories.TryAdd(key, factoryId))
		{
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
			Sink.HandleArrival(new(this, key, typeId));
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

	protected override Task StartAsync(IDiscoverySink<RootComponentKey, RootComponentDiscoveryContext, RootComponentCreationContext> sink, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _pendingArrivals) is not { } pendingArrival) goto Failed;

		lock (pendingArrival)
		{
			if (Interlocked.Exchange(ref _pendingArrivals, null) is null) goto Failed;
		}

		foreach (var (key, typeId) in pendingArrival)
		{
			sink.HandleArrival(new(this, key, typeId));
		}

		return Task.CompletedTask;
	Failed:;
		return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The service has already been initialized.")));
	}
}
