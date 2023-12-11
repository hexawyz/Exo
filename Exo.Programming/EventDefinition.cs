using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EventDefinition : NamedElement
{
	public EventDefinition(Guid id, string name, string comment, EventOptions options, ImmutableArray<ParameterDefinition> parameters) : base(id, name, comment)
	{
		Options = options;
		Parameters = parameters;
	}

	[DataMember(Order = 4)]
	public EventOptions Options { get; }
	[DataMember(Order = 5)]
	public ImmutableArray<ParameterDefinition> Parameters { get; }
}
