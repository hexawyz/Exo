using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class EffectTypeReference
{
	[DataMember(Order = 1)]
	public required string TypeName { get; init; }
}
