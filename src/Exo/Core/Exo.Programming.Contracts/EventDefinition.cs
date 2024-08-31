using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EventDefinition : NamedElement
{
	[DataMember(Order = 4)]
	public required EventOptions Options { get; init; }
	[DataMember(Order = 5)]
	public Guid ParametersTypeId { get; init; }
}
