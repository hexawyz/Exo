using System.Collections.Immutable;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class RootComponentCreationContext : ComponentCreationContext
{
	private readonly RootDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<RootComponentKey> Keys { get; }
	public override INestedDriverRegistryProvider DriverRegistry => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;
	public IDiscoveryOrchestrator DiscoveryOrchestrator => _discoverySubsystem.DiscoveryOrchestrator;
	public IDeviceNotificationService DeviceNotificationService => _discoverySubsystem.DeviceNotificationService;

	public RootComponentCreationContext(RootDiscoverySubsystem discoverySubsystem, ImmutableArray<RootComponentKey> keys)
	{
		_discoverySubsystem = discoverySubsystem;
		Keys = keys;
	}
}
