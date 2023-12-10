using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class ParameterDefinition : NamedElement
{
	public ParameterDefinition(Guid id, string name, string comment, Guid typeId) : base(id, name, comment)
	{
		TypeId = typeId;
	}

	[DataMember(Order = 4)]
	public Guid TypeId { get; }
}
