using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public abstract class NamedElement
{
	protected NamedElement(Guid id, string name, string comment)
	{
		Id = id;
		Name = name;
		Comment = comment;
	}

	[DataMember(Order = 1)]
	public Guid Id { get; }
	[DataMember(Order = 2)]
	public string Name { get; }
	[DataMember(Order = 3)]
	public string Comment { get; }
}
