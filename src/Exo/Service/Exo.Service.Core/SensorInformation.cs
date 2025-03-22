using Exo.Primitives;

namespace Exo.Service;

internal record struct SensorInformation
{
	public SensorInformation(Guid sensorId, SensorDataType dataType, SensorCapabilities capabilities, string unit, VariantNumber scaleMinimumValue, VariantNumber scaleMaximumValue)
	{
		SensorId = sensorId;
		DataType = dataType;
		Unit = unit;
		Capabilities = capabilities;
		ScaleMinimumValue = scaleMinimumValue;
		ScaleMaximumValue = scaleMaximumValue;
	}

	public Guid SensorId { get; }
	public SensorDataType DataType { get; }
	public SensorCapabilities Capabilities { get; }
	public string Unit { get; }
	public VariantNumber ScaleMinimumValue { get; }
	public VariantNumber ScaleMaximumValue { get; }
}
