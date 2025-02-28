using System.Collections.Immutable;
using System.Runtime.InteropServices;
using DeviceTools;

namespace Exo.Discovery;

public sealed class DnsSdDiscoveryContext : IComponentDiscoveryContext<DnsSdInstanceId, DnsSdDriverCreationContext>
{
	private readonly DnsSdDiscoverySubsystem _discoverySubsystem;
	private readonly DeviceObjectInformation _deviceObject;
	public ImmutableArray<DnsSdInstanceId> DiscoveredKeys { get; }

	internal DnsSdDiscoveryContext(DnsSdDiscoverySubsystem discoverySubsystem, DeviceObjectInformation deviceObject)
	{
		_discoverySubsystem = discoverySubsystem;
		_deviceObject = deviceObject;
		DiscoveredKeys = [deviceObject.Id];
	}

	public ValueTask<ComponentCreationParameters<DnsSdInstanceId, DnsSdDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		if (_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.ServiceName.Key, out string? serviceName))
		{
			var factories = _discoverySubsystem.ResolveFactories(serviceName);

			if (factories.Length > 0)
			{
				if (_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.FullName.Key, out string? fullName) &&
					_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.Domain.Key, out string? domain) &&
					_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.InstanceName.Key, out string? instanceName) &&
					_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.HostName.Key, out string? hostName) &&
					_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.PortNumber.Key, out ushort portNumber) &&
					_deviceObject.Properties.TryGetValue(Properties.System.Devices.Dnssd.TextAttributes.Key, out string[]? textAttributes))
				{
					return new
					(
						new ComponentCreationParameters<DnsSdInstanceId, DnsSdDriverCreationContext>
						(
							DiscoveredKeys,
							new
							(
								_discoverySubsystem,
								DiscoveredKeys,
								new
								(
									fullName,
									hostName,
									portNumber,
									domain,
									serviceName,
									instanceName,
									textAttributes is not null ? ImmutableCollectionsMarshal.AsImmutableArray(textAttributes) : []
								)
							),
							factories
						)
					);
				}
			}
		}
		return new(new ComponentCreationParameters<DnsSdInstanceId, DnsSdDriverCreationContext>(DiscoveredKeys, null!, []));
	}
}
