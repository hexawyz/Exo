using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly struct EmbeddedMonitorGraphicsDescription
{
	[DataMember(Order = 1)]
	public Guid GraphicsId { get; init; }
	[DataMember(Order = 2)]
	public Guid NameStringId { get; init; }
}
