using Exo.Primitives;

namespace Exo.Contracts.Ui.Settings;

public readonly struct SensorInformation
{
	public required Guid SensorId { get; init; }
	public required SensorDataType DataType { get; init; }
	public required string Unit { get; init; }
	public required SensorCapabilities Capabilities { get; init; }
	/// <summary>Gets the minimum value that should be used for the scale display.</summary>
	/// <remarks>This will ideally match the actual (theoretical) minimum value, but it is not guaranteed.</remarks>
	public VariantNumber ScaleMinimumValue { get; init; }
	/// <summary>Gets the maximum value that should be used for the scale display.</summary>
	/// <remarks>This will ideally match the actual (theoretical) maximum value, but it is not guaranteed.</remarks>
	public VariantNumber ScaleMaximumValue { get; init; }
}
