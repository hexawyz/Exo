using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class BatteryChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required float? Level { get; init; }

	[DataMember(Order = 3)]
	public required BatteryStatus BatteryStatus { get; init; }

	[DataMember(Order = 4)]
	public required ExternalPowerStatus ExternalPowerStatus { get; init; }
}
