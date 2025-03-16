using System.Collections.Immutable;
using DeviceTools.Processors;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class CpuDriverCreationContext : DriverCreationContext
{
	private readonly CpuDiscoverySubsystem _discoverySubsystem;

	public ImmutableArray<SystemCpuDeviceKey> Keys { get; }
	public X86VendorId VendorId { get; }
	public int ProcessorIndex { get; }
	public ProcessorPackageInformation PackageInformation => _discoverySubsystem.ProcessorPackages[ProcessorIndex];

	protected override INestedDriverRegistryProvider NestedDriverRegistryProvider => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;

	public CpuDriverCreationContext
	(
		CpuDiscoverySubsystem discoverySubsystem,
		ImmutableArray<SystemCpuDeviceKey> discoveredKeys,
		X86VendorId vendorId,
		int processorIndex
	)
	{
		_discoverySubsystem = discoverySubsystem;
		Keys = discoveredKeys;
		VendorId = vendorId;
		ProcessorIndex = processorIndex;
	}
}
