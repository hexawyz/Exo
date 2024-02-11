using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct DiscoveredAssemblyDetails
{
	public ImmutableArray<(string TypeName, ImmutableArray<(Guid Id, string Method, ImmutableArray<TypeReference> DiscoverySubsystems)>)> FactoryMethods { get; }

	public DiscoveredAssemblyDetails(ImmutableArray<(string TypeName, ImmutableArray<(Guid Id, string Method, ImmutableArray<TypeReference> DiscoverySubsystems)>)> factoryMethods)
		=> FactoryMethods = factoryMethods;
}
