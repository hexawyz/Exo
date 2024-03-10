using System.Collections.Immutable;
using Exo.SystemManagementBus;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class RamModuleDriverCreationContext : DriverCreationContext
{
	private readonly SystemManagementBiosRamDiscoverySubsystem _discoverySubsystem;

	public ImmutableArray<SystemMemoryDeviceKey> DiscoveredKeys { get; }
	public ImmutableArray<MemoryModuleInformation> MemoryModules { get; }
	public ISystemManagementBus SystemManagementBus { get; }

	protected override INestedDriverRegistryProvider NestedDriverRegistryProvider => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;

	public RamModuleDriverCreationContext
	(
		SystemManagementBiosRamDiscoverySubsystem discoverySubsystem,
		ImmutableArray<SystemMemoryDeviceKey> discoveredKeys,
		ImmutableArray<MemoryModuleInformation> memoryModules,
		ISystemManagementBus systemManagementBus
	)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = discoveredKeys;
		MemoryModules = memoryModules;
		SystemManagementBus = systemManagementBus;
	}
}
