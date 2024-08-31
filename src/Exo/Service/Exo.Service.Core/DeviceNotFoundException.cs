namespace Exo.Service;

// This exception should be thrown when a device that was explicitly requested by ID was not found in the configuration.
// This would generally indicate a configuration problem, as devices that don't have a configuration entry should not be referenced elsewhere.
public class DeviceNotFoundException : Exception
{
	public DeviceNotFoundException() : this("The requested device was not found.")
	{
	}

	public DeviceNotFoundException(string? message) : base(message)
	{
	}
}
