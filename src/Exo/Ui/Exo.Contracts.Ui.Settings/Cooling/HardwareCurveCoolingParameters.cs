using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class HardwareCurveCoolingParameters : ICurveCoolingParameters
{
	[DataMember(Order = 1)]
	public required Guid CoolingDeviceId { get; init; }
	Guid ICoolingParameters.DeviceId => CoolingDeviceId;
	[DataMember(Order = 2)]
	public required Guid CoolerId { get; init; }
	[DataMember(Order = 3)]
	public required Guid SensorId { get; init; }

	[DataMember(Order = 4)]
	public required CoolingControlCurve ControlCurve { get; init; }
}
