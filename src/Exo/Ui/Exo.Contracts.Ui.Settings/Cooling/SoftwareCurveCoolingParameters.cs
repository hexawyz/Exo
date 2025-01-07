using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class SoftwareCurveCoolingParameters : ICurveCoolingParameters
{
	[DataMember(Order = 1)]
	public required Guid CoolingDeviceId { get; init; }
	Guid ICoolingParameters.DeviceId => CoolingDeviceId;
	[DataMember(Order = 2)]
	public required Guid CoolerId { get; init; }
	[DataMember(Order = 3)]
	public required Guid SensorDeviceId { get; init; }
	[DataMember(Order = 4)]
	public required Guid SensorId { get; init; }

	/// <summary>Value to use when the sensor data is unavailable.</summary>
	[DataMember(Order = 5)]
	public required byte FallbackValue { get; init; }

	[DataMember(Order = 6)]
	public required CoolingControlCurve ControlCurve { get; init; }
}
