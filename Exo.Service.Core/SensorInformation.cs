namespace Exo.Service;

public record struct SensorInformation
{
	public SensorInformation(Guid sensorId, SensorDataType dataType, string unit, bool isPolled)
	{
		SensorId = sensorId;
		DataType = dataType;
		Unit = unit;
		IsPolled = isPolled;
	}

	public Guid SensorId { get; }
	public SensorDataType DataType { get; }
	public string Unit { get; }
	public bool IsPolled { get; }
}
