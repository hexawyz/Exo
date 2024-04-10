namespace Exo.Service;

public record struct SensorInformation
{
	public SensorInformation(Guid sensorId, SensorDataType dataType, bool isPolled)
	{
		SensorId = sensorId;
		DataType = dataType;
		IsPolled = isPolled;
	}

	public Guid SensorId { get; }
	public SensorDataType DataType { get; }
	public bool IsPolled { get; }
}
