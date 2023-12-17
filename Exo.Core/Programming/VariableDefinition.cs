using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class VariableDefinition : NamedElement
{
	public VariableDefinition(Guid id, string name, string comment, Guid typeId) : base(id, name, comment)
	{
		TypeId = typeId;
	}

	[DataMember(Order = 4)]
	public Guid TypeId { get; }
}
