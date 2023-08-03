using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class EffectTypeReference
{
	[DataMember(Order = 1)]
	public required Guid TypeId { get; init; }
}
