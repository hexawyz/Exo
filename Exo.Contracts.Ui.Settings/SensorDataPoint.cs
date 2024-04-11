using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly struct SensorDataPoint
{
	[DataMember(Order = 1)]
	public required DateTime DateTime { get; init; }
	[DataMember(Order = 2)]
	public required double Value { get; init; }
}
