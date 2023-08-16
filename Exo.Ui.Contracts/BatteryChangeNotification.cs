using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class BatteryChangeNotification
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2)]
	public required float BatteryLevel { get; init; }
}
