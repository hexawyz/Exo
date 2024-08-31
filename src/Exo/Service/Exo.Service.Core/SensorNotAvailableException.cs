namespace Exo.Service;

public class SensorNotAvailableException : Exception
{
	public SensorNotAvailableException() : this("The sensor is not available.")
	{
	}

	public SensorNotAvailableException(string? message) : base(message)
	{
	}
}
