using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EffectTypeReference
{
	[DataMember(Order = 1)]
	public required Guid TypeId { get; init; }
}
