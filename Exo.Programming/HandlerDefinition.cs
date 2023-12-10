using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class HandlerDefinition : NamedElement
{
	public HandlerDefinition(Guid id, string name, string comment) : base(id, name, comment)
	{
	}

	// Should be something like ImpureCallExpression
	//public ImmutableArray<object> Actions { get; }
}
