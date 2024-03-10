using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class RamModuleDiscoveryContext : IComponentDiscoveryContext<SystemMemoryDeviceKey, RamModuleDriverCreationContext>
{
	private readonly SystemManagementBiosRamDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemMemoryDeviceKey> DiscoveredKeys { get; }
	private readonly ImmutableArray<MemoryModuleInformation> _memoryModules;
	private readonly Guid _factoryId;

	public RamModuleDiscoveryContext
	(
		SystemManagementBiosRamDiscoverySubsystem discoverySubsystem,
		ImmutableArray<MemoryModuleInformation> memoryModules,
		Guid factoryId
	)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = ImmutableArray.CreateRange(memoryModules, m => (SystemMemoryDeviceKey)m.Index);
		_memoryModules = memoryModules;
		_factoryId = factoryId;
	}

	public ValueTask<ComponentCreationParameters<SystemMemoryDeviceKey, RamModuleDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
		=> ValueTask.FromResult(new ComponentCreationParameters<SystemMemoryDeviceKey, RamModuleDriverCreationContext>(DiscoveredKeys, new(_discoverySubsystem, DiscoveredKeys, _memoryModules), [_factoryId]));
}
