using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class VariableDefinition : NamedElement
{
	[DataMember(Order = 4)]
	public required Guid TypeId { get; init; }
}
