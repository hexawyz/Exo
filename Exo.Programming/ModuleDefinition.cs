using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class ModuleDefinition : NamedElement
{
	public ModuleDefinition(Guid id, string name, string comment, ImmutableArray<EventDefinition> events) : base(id, name, comment)
	{
		Events = events;
	}

	[DataMember(Order = 4)]
	public ImmutableArray<EventDefinition> Events { get; }
}
