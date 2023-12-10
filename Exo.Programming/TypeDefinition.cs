using System.Runtime.Serialization;

namespace Exo.Programming;

// Type definitions could be user-defined or map to other well-known types of the current model.
[DataContract]
public sealed class TypeDefinition : NamedElement
{
	public TypeDefinition(Guid id, string name, string comment) : base(id, name, comment)
	{
	}

	//[DataContract(Order = 4)]
	//public ImmutableArray<FieldDefinition> Fields { get; }
}
