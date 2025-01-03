namespace Exo.Service;

internal record struct SensorInformation
{
	public SensorInformation(Guid sensorId, SensorDataType dataType, string unit, SensorCapabilities capabilities, object? scaleMinimumValue, object? scaleMaximumValue)
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
	public string Unit { get; }
	public SensorCapabilities Capabilities { get; init; }
	public object? ScaleMinimumValue { get; }
	public object? ScaleMaximumValue { get; }
}
