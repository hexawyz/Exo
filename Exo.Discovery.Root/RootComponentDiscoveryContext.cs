using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class RootComponentDiscoveryContext : IComponentDiscoveryContext<RootComponentKey, RootComponentCreationContext>
{
	private readonly RootDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<RootComponentKey> DiscoveredKeys { get; }

	internal RootComponentDiscoveryContext(RootDiscoverySubsystem discoverySubsystem, RootComponentKey key)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [key];
	}

	public ValueTask<ComponentCreationParameters<RootComponentKey, RootComponentCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
		=> new
		(
			new ComponentCreationParameters<RootComponentKey, RootComponentCreationContext>
			(
				DiscoveredKeys,
				new RootComponentCreationContext(_discoverySubsystem, DiscoveredKeys),
				[_discoverySubsystem.RegisteredFactories[DiscoveredKeys[0]]]
			)
		);
}
