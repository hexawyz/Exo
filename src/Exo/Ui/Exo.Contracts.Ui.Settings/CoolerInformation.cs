using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class CoolerInformation
{
	[DataMember(Order = 1)]
	public required Guid CoolerId { get; init; }
	[DataMember(Order = 2)]
	public Guid? SpeedSensorId { get; init; }
	[DataMember(Order = 3)]
	public required CoolerType Type { get; init; }
	[DataMember(Order = 4)]
	public required CoolingModes SupportedCoolingModes { get; init; }
	[DataMember(Order = 5)]
	public CoolerPowerLimits? PowerLimits { get; init; }
}
