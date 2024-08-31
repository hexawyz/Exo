using System.Collections.Immutable;
using Exo.Discovery;

namespace Exo.Debug;

public sealed class DebugDiscoveryContext : IComponentDiscoveryContext<DebugDeviceKey, DebugDriverCreationContext>
{
	private readonly DebugDiscoverySystem _discoverySubsystem;

	public ImmutableArray<DebugDeviceKey> DiscoveredKeys { get; }

	internal DebugDiscoveryContext(DebugDiscoverySystem discoverySubsystem, ImmutableArray<DebugDeviceKey> discoveredKeys)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = discoveredKeys;
	}

	public ValueTask<ComponentCreationParameters<DebugDeviceKey, DebugDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
		=> new
		(
			new ComponentCreationParameters<DebugDeviceKey, DebugDriverCreationContext>
			(
				DiscoveredKeys,
				new(_discoverySubsystem.BuildDriver((Guid)DiscoveredKeys[0])),
				_discoverySubsystem.ResolveFactories((Guid)DiscoveredKeys[0])
			)
		);
}
