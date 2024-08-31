using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class ModuleDefinition : NamedElement
{
	[DataMember(Order = 4)]
	public ImmutableArray<TypeDefinition> Types { get; init; } = [];
	[DataMember(Order = 5)]
	public ImmutableArray<EventDefinition> Events { get; init; } = [];
}
