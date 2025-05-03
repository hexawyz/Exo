using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class CpuDiscoveryContext : IComponentDiscoveryContext<SystemCpuDeviceKey, CpuDriverCreationContext>
{
	private readonly CpuDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemCpuDeviceKey> DiscoveredKeys { get; }
	private readonly X86VendorId _vendorId;
	private readonly int _processorIndex;
	private readonly Guid _factoryId;

	public CpuDiscoveryContext
	(
		CpuDiscoverySubsystem discoverySubsystem,
		X86VendorId vendorId,
		int index,
		Guid factoryId
	)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [ (SystemCpuDeviceKey)index ];
		_vendorId = vendorId;
		_processorIndex = index;
		_factoryId = factoryId;
	}

	public ValueTask<ComponentCreationParameters<SystemCpuDeviceKey, CpuDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		try
		{
			return new(new ComponentCreationParameters<SystemCpuDeviceKey, CpuDriverCreationContext>(DiscoveredKeys, new(_discoverySubsystem, DiscoveredKeys, _vendorId, _processorIndex), [_factoryId]));
		}
		catch (Exception ex)
		{
			return ValueTask.FromException<ComponentCreationParameters<SystemCpuDeviceKey, CpuDriverCreationContext>>(ex);
		}
	}
}
