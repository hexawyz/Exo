using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorImageConfiguration
{
	[DataMember(Order = 1)]
	public required UInt128 ImageId { get; init; }
	[DataMember(Order = 2)]
	public required Rectangle ImageRegion { get; init; }
}
