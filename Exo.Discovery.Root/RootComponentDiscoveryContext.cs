using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class RootComponentDiscoveryContext : IComponentDiscoveryContext<RootComponentKey, RootComponentCreationContext>
{
	private readonly RootDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<RootComponentKey> DiscoveredKeys { get; }
	private readonly Guid _typeId;

	internal RootComponentDiscoveryContext(RootDiscoverySubsystem discoverySubsystem, RootComponentKey discoveredKey, Guid typeId)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [discoveredKey];
		_typeId = typeId;
	}

	public ValueTask<ComponentCreationParameters<RootComponentKey, RootComponentCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
		=> new
		(
			new ComponentCreationParameters<RootComponentKey, RootComponentCreationContext>
			(
				DiscoveredKeys,
				new RootComponentCreationContext(_discoverySubsystem, DiscoveredKeys, _typeId),
				[_discoverySubsystem.RegisteredFactories[DiscoveredKeys[0]]]
			)
		);
}
