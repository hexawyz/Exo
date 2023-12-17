using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EffectDefinition : NamedElement
{
	public EffectDefinition
	(
		Guid id,
		string name,
		string comment,
		ImmutableArray<EffectLayerDefinition> layers,
		ImmutableArray<VariableDefinition> variables,
		ImmutableArray<EventDefinition> events,
		ImmutableArray<EffectDefinition> children
	) : base(id, name, comment)
	{
		Layers = layers;
		Variables = variables;
		Events = events;
		Children = children;
	}

	//[DataMember(Order = 4)]
	//public ImmutableArray<ParameterDefinition> Parameters { get; }
	[DataMember(Order = 5)]
	public ImmutableArray<EffectLayerDefinition> Layers { get; }
	[DataMember(Order = 6)]
	public ImmutableArray<VariableDefinition> Variables { get; }
	[DataMember(Order = 7)]
	public ImmutableArray<EventDefinition> Events { get; }
	[DataMember(Order = 8)]
	public ImmutableArray<EffectDefinition> Children { get; }
	//[DataMember(Order = 8)]
	//public ImmutableArray<StateDefinition> States { get; }
}
