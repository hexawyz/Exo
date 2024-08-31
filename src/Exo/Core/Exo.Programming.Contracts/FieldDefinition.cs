using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class FieldDefinition : NamedElement
{
	[DataMember(Order = 4)]
	public required Guid TypeId { get; init; }
}
