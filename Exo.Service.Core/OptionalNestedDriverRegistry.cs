namespace Exo.Service;

public sealed class OptionalNestedDriverRegistry : Optional<IDriverRegistry>
{
	private readonly IDriverRegistry _registry;

	public OptionalNestedDriverRegistry(IDriverRegistry registry) => _registry = registry;

	protected override IDriverRegistry CreateValue() => _registry.CreateNestedRegistry();
}
