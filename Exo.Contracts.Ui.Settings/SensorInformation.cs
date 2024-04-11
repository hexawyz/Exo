using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class SensorInformation
{
	[DataMember(Order = 1)]
	public required Guid SensorId { get; init; }
	[DataMember(Order = 2)]
	public required SensorDataType DataType { get; init; }
	[DataMember(Order = 3)]
	public required bool IsPolled { get; init; }
}
