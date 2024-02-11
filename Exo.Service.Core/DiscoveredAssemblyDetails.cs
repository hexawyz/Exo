using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct DiscoveredAssemblyDetails
{
	public ImmutableArray<(string TypeName, ImmutableArray<(Guid Id, MethodSignature MethodSignature, ImmutableArray<TypeReference> DiscoverySubsystems)>)> FactoryMethods { get; }

	public DiscoveredAssemblyDetails(ImmutableArray<(string TypeName, ImmutableArray<(Guid Id, MethodSignature MethodSignature, ImmutableArray<TypeReference> DiscoverySubsystems)>)> factoryMethods)
		=> FactoryMethods = factoryMethods;
}
