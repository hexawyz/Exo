using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class ModuleDefinition : NamedElement
{
	public ModuleDefinition(Guid id, string name, string comment, ImmutableArray<TypeDefinition> types, ImmutableArray<EventDefinition> events) : base(id, name, comment)
	{
		Types = types;
		Events = events;
	}

	[DataMember(Order = 4)]
	public ImmutableArray<TypeDefinition> Types { get; }
	[DataMember(Order = 4)]
	public ImmutableArray<EventDefinition> Events { get; }
}
