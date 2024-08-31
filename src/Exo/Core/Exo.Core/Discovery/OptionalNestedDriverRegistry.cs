namespace Exo.Discovery;

public sealed class OptionalNestedDriverRegistry : Optional<IDriverRegistry>
{
	private readonly INestedDriverRegistryProvider _registry;

	public OptionalNestedDriverRegistry(INestedDriverRegistryProvider registry) => _registry = registry;

	protected override IDriverRegistry CreateValue() => _registry.CreateNestedRegistry();
}
