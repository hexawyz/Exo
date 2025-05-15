using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Exo.Discovery;
using Microsoft.Extensions.Logging;

namespace Exo.Debug;

/// <summary>Provides a discovery system to create virtual debug devices.</summary>
/// <remarks>
/// This tool will provide useful services to debug service features by allowing to freely trigger device arrivals and removals, as well as watch the fake device states.
/// </remarks>
[TypeId(0xF2402C50, 0x26BE, 0x45C2, 0xBC, 0x08, 0x1B, 0xE1, 0xB7, 0x2E, 0x94, 0x96)]
public class DebugDiscoverySystem : DiscoveryService<DebugDiscoverySystem, DebugDeviceKey, DebugFactoryDetails, DebugDiscoveryContext, DebugDriverCreationContext, Driver, DriverCreationResult<DebugDeviceKey>>, IJsonTypeInfoProvider<DebugFactoryDetails>
{
	static JsonTypeInfo<DebugFactoryDetails> IJsonTypeInfoProvider<DebugFactoryDetails>.JsonTypeInfo => SourceGenerationContext.Default.DebugFactoryDetails;

	internal ILoggerFactory LoggerFactory { get; }
	internal INestedDriverRegistryProvider DriverRegistry { get; }
	private readonly Lock _lock;
	private Guid _factoryId;

	public override string FriendlyName => "Debug Device Discovery";

	public static async ValueTask<DebugDiscoverySystem> CreateAsync
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry,
		IDiscoveryOrchestrator discoveryOrchestrator
	)
	{
		var service = new DebugDiscoverySystem(loggerFactory, driverRegistry);
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

	private DebugDiscoverySystem
	(
		ILoggerFactory loggerFactory,
		INestedDriverRegistryProvider driverRegistry
	)
	{
		LoggerFactory = loggerFactory;
		DriverRegistry = driverRegistry;
		_lock = new();
	}

	protected override Task StartAsync(IDiscoverySink<DebugDeviceKey, DebugDiscoveryContext, DebugDriverCreationContext> sink, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	public override bool TryParseFactory(ImmutableArray<CustomAttributeData> attributes, [NotNullWhen(true)] out DebugFactoryDetails? parsedFactoryDetails)
	{
		foreach (var attribute in attributes)
		{
			if (attribute.Matches<DebugFactoryAttribute>())
			{
				parsedFactoryDetails = new();
				return true;
			}
		}
		parsedFactoryDetails = null;
		return false;
	}

	public override bool TryRegisterFactory(Guid factoryId, DebugFactoryDetails parsedFactoryDetails)
	{
		lock (_lock)
		{
			if (_factoryId != default) return false;
			_factoryId = factoryId;
			return true;
		}
	}

	internal ImmutableArray<Guid> ResolveFactories(Guid deviceId)
	{
		// TODO: Check that the device is registered.
		lock (_lock)
		{
			if (_factoryId != default)
			{
				return [_factoryId];
			}
		}

		return [];
	}

	public DebugDriver? BuildDriver(Guid deviceId)
	{
		// TODO: Create the driver based on one of the virtual devices if available.
		return null;
	}
}
