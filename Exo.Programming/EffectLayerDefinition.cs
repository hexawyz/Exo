using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public sealed class EffectLayerDefinition : NamedElement
{
	public EffectLayerDefinition(Guid id, string name, string comment) : base(id, name, comment) { }
}
