namespace Exo.Service;

public class MonitorNotFoundException : Exception
{
	public MonitorNotFoundException() : this("The requested monitor was not found on the device.")
	{
	}

	public MonitorNotFoundException(string? message) : base(message)
	{
	}
}
