using System.Collections.Immutable;

namespace Exo.Service;

[TypeId(0xF60E44BC, 0x67E5, 0x49AF, 0x94, 0x22, 0x1D, 0xC1, 0xBA, 0x33, 0x1B, 0x02)]
internal readonly struct DiscoveredAssemblyDetails
{
	public ImmutableArray<DiscoveredTypeDetails> Types { get; init; }

	public DiscoveredAssemblyDetails(ImmutableArray<DiscoveredTypeDetails> types)
		=> Types = types;
}

internal readonly struct DiscoveredTypeDetails
{
	public string Name { get; init; }
	public ImmutableArray<DiscoveredFactoryMethodDetails> FactoryMethods { get; init; }

	public DiscoveredTypeDetails(string typeName, ImmutableArray<DiscoveredFactoryMethodDetails> factoryMethods)
	{
		Name = typeName;
		FactoryMethods = factoryMethods;
	}
}

internal readonly struct DiscoveredFactoryMethodDetails
{
	public Guid Id { get; init; }
	public MethodSignature MethodSignature { get; init; }
	public ImmutableArray<TypeReference> DiscoverySubsystemTypes { get; init; }

	public DiscoveredFactoryMethodDetails(Guid id, MethodSignature methodSignature, ImmutableArray<TypeReference> discoverySubsystems)
	{
		Id = id;
		MethodSignature = methodSignature;
		DiscoverySubsystemTypes = discoverySubsystems;
	}
}
