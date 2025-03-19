namespace Exo.Service;

public class SensorNotFoundException : Exception
{
	public SensorNotFoundException() : this("The requested sensor was not found on the device.")
	{
	}

	public SensorNotFoundException(string? message) : base(message)
	{
	}
}

