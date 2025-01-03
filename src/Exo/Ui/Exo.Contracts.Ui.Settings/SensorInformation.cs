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
	public required string Unit { get; init; }
	[DataMember(Order = 4)]
	public required SensorCapabilities Capabilities { get; init; }
	/// <summary>Gets the minimum value that should be used for the scale display.</summary>
	/// <remarks>This will ideally match the actual (theoretical) minimum value, but it is not guaranteed.</remarks>
	[DataMember(Order = 5)]
	public double? ScaleMinimumValue { get; init; }
	/// <summary>Gets the maximum value that should be used for the scale display.</summary>
	/// <remarks>This will ideally match the actual (theoretical) maximum value, but it is not guaranteed.</remarks>
	[DataMember(Order = 6)]
	public double? ScaleMaximumValue { get; init; }
}
