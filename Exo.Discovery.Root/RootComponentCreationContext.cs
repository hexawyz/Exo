using System.Collections.Immutable;
using Exo.I2C;
using Exo.Services;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

public sealed class RootComponentCreationContext : ComponentCreationContext
{
	private readonly RootDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<RootComponentKey> Keys { get; }
	private readonly Guid _typeId;

	public override INestedDriverRegistryProvider DriverRegistry => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;
	public IDiscoveryOrchestrator DiscoveryOrchestrator => _discoverySubsystem.DiscoveryOrchestrator;
	public IDeviceNotificationService DeviceNotificationService => _discoverySubsystem.DeviceNotificationService;
	public II2CBusProvider I2cBusProvider => _discoverySubsystem.I2CBusProvider;
	//public IConfigurationContainer ConfigurationContainer
	//	=> _typeId != default ?
	//		_discoverySubsystem.DiscoveryConfigurationContainer.GetContainer(_typeId) :
	//		throw new InvalidOperationException($"No type ID was specified for {Keys[0]}");

	public RootComponentCreationContext(RootDiscoverySubsystem discoverySubsystem, ImmutableArray<RootComponentKey> keys, Guid typeId)
	{
		_discoverySubsystem = discoverySubsystem;
		Keys = keys;
		_typeId = typeId;
	}
}
