namespace Exo.Service;

public record struct SensorInformation
{
	public SensorInformation(Guid sensorId, SensorDataType dataType, string unit, bool isPolled, object? scaleMinimumValue, object? scaleMaximumValue)
	{
		SensorId = sensorId;
		DataType = dataType;
		Unit = unit;
		IsPolled = isPolled;
		ScaleMinimumValue = scaleMinimumValue;
		ScaleMaximumValue = scaleMaximumValue;
	}

	public Guid SensorId { get; }
	public SensorDataType DataType { get; }
	public string Unit { get; }
	public bool IsPolled { get; }
	public object? ScaleMinimumValue { get; }
	public object? ScaleMaximumValue { get; }
}
