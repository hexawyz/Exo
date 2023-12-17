using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EffectDefinition : NamedElement
{
	//[DataMember(Order = 4)]
	//public ImmutableArray<ParameterDefinition> Parameters { get; } = [];
	[DataMember(Order = 5)]
	public ImmutableArray<EffectLayerDefinition> Layers { get; init; } = [];
	[DataMember(Order = 6)]
	public ImmutableArray<VariableDefinition> Variables { get; init; } = [];
	[DataMember(Order = 7)]
	public ImmutableArray<EventDefinition> Events { get; init; } = [];
	[DataMember(Order = 8)]
	public ImmutableArray<EffectDefinition> Children { get; init; } = [];
	//[DataMember(Order = 8)]
	//public ImmutableArray<StateDefinition> States { get; } = [];
}
