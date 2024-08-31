using System.Collections.Immutable;
using Exo.SystemManagementBus;

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

	public async ValueTask<ComponentCreationParameters<SystemMemoryDeviceKey, RamModuleDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		// Similarly to what is done for monitors, we wait some amount of time for an SMBus implementation to become available.
		// Initially, there won't be SMBus implementations for all hardware, so this is likely to fail, but over time, hopefully, we can have valid SMBus implementations for all systems.
		ISystemManagementBus? smBus;
		using (var smBusTimeoutCancellationTokenSource = new CancellationTokenSource(new TimeSpan(60 * TimeSpan.TicksPerSecond)))
		using (var hybridCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, smBusTimeoutCancellationTokenSource.Token))
		{
			try
			{
				smBus = await _discoverySubsystem.SystemManagementBusProvider.GetSystemBusAsync(hybridCancellationTokenSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException ocex) when (smBusTimeoutCancellationTokenSource.IsCancellationRequested)
			{
				throw new InvalidOperationException($"Could not resolve the SMBus implementation for the system in the given period of time. This is likely due to the lack of a provider.");
			}
		}
		await _discoverySubsystem.SystemManagementBusProvider.GetSystemBusAsync(cancellationToken);
		return new ComponentCreationParameters<SystemMemoryDeviceKey, RamModuleDriverCreationContext>(DiscoveredKeys, new(_discoverySubsystem, DiscoveredKeys, _memoryModules, smBus), [_factoryId]);
	}
}
