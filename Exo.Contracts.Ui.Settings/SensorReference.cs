using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly struct SensorReference
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required Guid SensorId { get; init; }
}
