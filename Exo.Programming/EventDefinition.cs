using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EventDefinition : NamedElement
{
	public EventDefinition(Guid id, string name, string comment, EventOptions options) : base(id, name, comment)
	{
		Options = options;
	}

	[DataMember(Order = 4)]
	public EventOptions Options { get; }
	//[DataMember(Order = 5)]
	//public ImmutableArray<ParameterDefinition> Parameters { get; }
}
